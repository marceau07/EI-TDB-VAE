using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using EiVae.Api.Endpoints;
using EiVae.Api.Services;
using EiVae.Domain.Services;
using EiVae.Infrastructure;
using EiVae.Infrastructure.Documents;
using EiVae.Infrastructure.Integrations.FranceCompetences;
using EiVae.Infrastructure.Integrations.FranceVae;
using EiVae.Infrastructure.Seeding;
using EiVae.Infrastructure.Services;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Tous les formats affichés — dates, montants, nombres — suivent la convention
// française. La culture est fixée explicitement pour que le serveur se comporte
// de la même manière quelle que soit la configuration de la machine hôte.
var cultureFr = CultureInfo.GetCultureInfo("fr-FR");
CultureInfo.DefaultThreadCurrentCulture = cultureFr;
CultureInfo.DefaultThreadCurrentUICulture = cultureFr;

// ---------------------------------------------------------------- persistance
var connexion = builder.Configuration.GetConnectionString("Postgres")
    ?? "Host=localhost;Port=5432;Database=eivae;Username=eivae;Password=eivae";

builder.Services.AddDbContext<VaeDbContext>(o =>
{
    o.UseNpgsql(connexion, npg => npg.MigrationsAssembly(typeof(VaeDbContext).Assembly.FullName));
    if (builder.Environment.IsDevelopment())
    {
        o.EnableDetailedErrors();
    }
});

// ---------------------------------------------------------------- options
builder.Services.Configure<FranceCompetencesOptions>(
    builder.Configuration.GetSection(FranceCompetencesOptions.Section));
builder.Services.Configure<FranceVaeOptions>(
    builder.Configuration.GetSection(FranceVaeOptions.Section));
builder.Services.Configure<SharePointOptions>(
    builder.Configuration.GetSection(SharePointOptions.Section));
builder.Services.Configure<SiteWebOptions>(
    builder.Configuration.GetSection(SiteWebOptions.Section));
builder.Services.Configure<NotificationsOptions>(
    builder.Configuration.GetSection(NotificationsOptions.Section));
builder.Services.Configure<DossiersCandidatsOptions>(o =>
{
    builder.Configuration.GetSection(DossiersCandidatsOptions.Section).Bind(o);

    // Un chemin relatif se lit depuis le dossier de l'application, pas depuis
    // le répertoire courant du processus, qui varie selon le mode de lancement.
    if (o.EstConfigure && !Path.IsPathRooted(o.Racine))
    {
        o.Racine = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, o.Racine));
    }
});

builder.Services.AddSingleton(sp =>
{
    var o = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<SharePointOptions>>().Value;
    return new SharePointLinkBuilder(o);
});

// ---------------------------------------------------------------- services
builder.Services.AddHttpClient(nameof(FranceCompetencesImporter), c =>
{
    // L'archive RNCP pèse environ 75 Mo : le délai par défaut de 100 s ne suffit pas.
    c.Timeout = TimeSpan.FromMinutes(15);
});
builder.Services.AddHttpClient(nameof(FranceVaeClient), c => c.Timeout = TimeSpan.FromSeconds(30));

builder.Services.AddScoped<ParametresService>();
builder.Services.AddScoped<ServiceAlertes>();
builder.Services.AddScoped<AffectationService>();
builder.Services.AddScoped<ReferentielSeeder>();
builder.Services.AddScoped<DonneesInitialesSeeder>();
builder.Services.AddScoped<FranceCompetencesImporter>();
builder.Services.AddScoped<FranceVaeClient>();
builder.Services.AddScoped<ImportCandidaturesCsv>();
builder.Services.AddScoped<ServiceTableauDeBord>();
builder.Services.AddScoped<ServiceNotifications>();
builder.Services.AddScoped<GenerateurDossierCandidat>();

builder.Services.AddHostedService<SynchronisationQuotidienne>();
builder.Services.AddHostedService<EnvoiNotifications>();

builder.Services.Configure<JsonOptions>(o =>
{
    o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    o.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
});

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddResponseCompression(o => o.EnableForHttps = true);

var origines = builder.Configuration.GetSection("Cors:Origines").Get<string[]>() ?? [];
if (origines.Length > 0)
{
    builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
        .WithOrigins(origines).AllowAnyHeader().AllowAnyMethod()));
}

var app = builder.Build();

// ---------------------------------------------------------------- démarrage
await using (var portee = app.Services.CreateAsyncScope())
{
    var db = portee.ServiceProvider.GetRequiredService<VaeDbContext>();
    var log = portee.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Demarrage");

    if (app.Configuration.GetValue("Base:MigrerAuDemarrage", true))
    {
        try
        {
            await db.Database.MigrateAsync();
            log.LogInformation("Base à jour.");

            await portee.ServiceProvider.GetRequiredService<ReferentielSeeder>().AmorcerAsync();

            if (app.Configuration.GetValue("Base:ChargerDonneesInitiales", true))
            {
                await portee.ServiceProvider.GetRequiredService<DonneesInitialesSeeder>().ChargerAsync();
            }

            // Les règles ont pu changer depuis le dernier démarrage : le plan
            // d'action est recalculé avant de servir la première page.
            await portee.ServiceProvider.GetRequiredService<ServiceAlertes>().RafraichirAsync();
        }
        catch (Exception ex)
        {
            // Un serveur qui démarre sans base doit le dire clairement plutôt que
            // de servir des pages vides : l'API répondra 503 sur les routes de données.
            log.LogError(ex, "La base n'a pas pu être préparée. Vérifiez la chaîne de connexion PostgreSQL.");
        }
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler();
    app.UseHsts();
}

app.UseResponseCompression();
app.UseStatusCodePages();

if (origines.Length > 0)
{
    app.UseCors();
}

app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    // L'interface évolue avec l'application : le navigateur revalide chaque
    // fichier (réponse 304 s'il n'a pas changé) au lieu de servir une version
    // périmée après une mise à jour.
    OnPrepareResponse = ctx => ctx.Context.Response.Headers.CacheControl = "no-cache",
});

app.MapOpenApi();
app.MapScalarApiReference("/api/doc", o => o
    .WithTitle("API Pilotage VAE — EI Groupe")
    .WithTheme(ScalarTheme.BluePlanet));

app.MapDashboardEndpoints();
app.MapCandidatsEndpoints();
app.MapIntervenantsEndpoints();
app.MapCertificationsEndpoints();
app.MapImportsEndpoints();
app.MapReferentielEndpoints();
app.MapCollectiveEndpoints();
app.MapNotificationsEndpoints();
app.MapElearningEndpoints();

app.MapGet("/api/sante", async (VaeDbContext db, CancellationToken ct) =>
{
    var accessible = await db.Database.CanConnectAsync(ct);
    return accessible
        ? Results.Ok(new { statut = "ok", base_donnees = "connectée", horodatage = DateTimeOffset.UtcNow })
        : Results.Json(new { statut = "degrade", base_donnees = "injoignable" }, statusCode: 503);
}).WithSummary("État du service et de la base de données.");

app.MapFallbackToFile("index.html");

await app.RunAsync();

/// <summary>Rendue publique pour les tests d'intégration.</summary>
public partial class Program;
