using System.Text.Json;
using EiVae.Domain;
using EiVae.Domain.Entities;
using EiVae.Infrastructure;
using EiVae.Infrastructure.Integrations.FranceCompetences;
using EiVae.Infrastructure.Integrations.FranceVae;
using EiVae.Infrastructure.Documents;
using EiVae.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EiVae.Api.Endpoints;

/// <summary>Configuration du formulaire de candidature du site groupe-ei.fr.</summary>
public sealed class SiteWebOptions
{
    public const string Section = "SiteWeb";

    /// <summary>
    /// Secret partagé, transmis par le site dans l'en-tête X-EiVae-Secret.
    /// Sans lui, le point d'entrée refuse toute demande : il est public.
    /// </summary>
    public string? Secret { get; set; }

    public string UrlFormulaire { get; set; } = "https://www.groupe-ei.fr/vae/";

    public bool EstConfigure => !string.IsNullOrWhiteSpace(Secret);
}

/// <summary>Charge utile attendue du formulaire du site.</summary>
public sealed record DemandeSiteWeb(
    string Nom, string Prenom, string? Email, string? Telephone,
    string? CodePostal, string? Ville, string? Certification, string? Message, string? Source);

public sealed record QualifierDemande(int? CertificationId, int? AapId, bool Rejeter, string? Commentaire);

public static class ImportsEndpoints
{
    public static void MapImportsEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/integrations").WithTags("Intégrations");

        // ============================================================ France Compétences
        g.MapGet("/france-competences/etat", async (
                VaeDbContext db, FranceCompetencesImporter importeur,
                IOptions<FranceCompetencesOptions> options, CancellationToken ct) =>
        {
            var dernier = await db.Imports
                .Where(i => i.Source == SourceImport.FranceCompetences)
                .OrderByDescending(i => i.DemarreLe).FirstOrDefaultAsync(ct);

            (string Url, string Titre)? disponible = null;
            string? erreur = null;
            try
            {
                disponible = await importeur.TrouverDernierExportAsync(ct);
            }
            catch (Exception ex)
            {
                erreur = ex.Message;
            }

            return Results.Ok(new
            {
                source = "France Compétences — export RNCP publié sur data.gouv.fr",
                acces = "libre, sans authentification",
                options.Value.DatasetUrl,
                exportDisponible = disponible?.Titre,
                erreur,
                synchronisationAutomatique = options.Value.ActiverSynchronisationAutomatique,
                heure = options.Value.HeureSynchronisation.ToString("HH\\hmm"),
                dernierImport = dernier is null ? null : new
                {
                    dernier.DemarreLe, dernier.TermineLe, statut = dernier.Statut.ToString(),
                    dernier.Reference, dernier.NombreLus, dernier.NombreCrees,
                    dernier.NombreMisAJour, dernier.NombreIgnores, dernier.NombreErreurs,
                },
                champsSynchronises = FranceCompetencesImporter.ChampsSynchronises,
                champsInternes = FranceCompetencesImporter.ChampsInternes,
                certificationsEnBase = await db.Certifications.CountAsync(ct),
                certificationsSynchronisees =
                    await db.Certifications.CountAsync(c => c.DerniereSynchronisation != null, ct),
                blocsEnBase = await db.Blocs.CountAsync(ct),
            });
        }).WithSummary("État de la synchronisation du référentiel RNCP.");

        g.MapPost("/france-competences/synchroniser", async (
                FranceCompetencesImporter importeur, CancellationToken ct) =>
        {
            var run = await importeur.SynchroniserAsync("manuel", ct);
            return Results.Ok(new
            {
                run.Id, statut = run.Statut.ToString(), run.Reference,
                run.NombreLus, run.NombreCrees, run.NombreMisAJour, run.NombreIgnores, run.NombreErreurs,
                journal = run.Journal?.Split('\n'),
            });
        }).WithSummary("Lance immédiatement la synchronisation du référentiel RNCP.");

        // ============================================================ France VAE
        g.MapGet("/france-vae/etat", async (
                FranceVaeClient client, IOptions<FranceVaeOptions> options,
                VaeDbContext db, CancellationToken ct) =>
        {
            var (ok, message) = client.EstConfigure
                ? await client.TesterConnexionAsync(ct)
                : (false, "Aucun jeton n'est configuré.");

            var dernier = await db.Imports
                .Where(i => i.Source == SourceImport.FranceVaeExport || i.Source == SourceImport.FranceVaeApi)
                .OrderByDescending(i => i.DemarreLe).FirstOrDefaultAsync(ct);

            return Results.Ok(new
            {
                configure = client.EstConfigure,
                connexion = new { ok, message },
                options.Value.BaseUrl,
                options.Value.Prefixe,

                // La portée réelle de l'API doit rester visible dans l'outil :
                // c'est ce qui évite qu'on attende d'elle ce qu'elle ne fait pas.
                portee = new
                {
                    routeDisponible = "GET /interop/v1/candidatures/{id}",
                    listing = false,
                    destinataire = "certificateurs",
                    remarque = "L'API d'interopérabilité France VAE n'expose pas de listing des candidatures. "
                               + "Elle permet de rafraîchir un dossier dont l'identifiant est déjà connu. "
                               + "L'alimentation du flux entrant passe par l'import de l'export du back-office.",
                },
                dossiersAvecIdentifiant = await db.Parcours.CountAsync(p => p.CandidatureFranceVaeId != null, ct),
                dernierImport = dernier is null ? null : new
                {
                    dernier.DemarreLe, statut = dernier.Statut.ToString(),
                    dernier.NombreCrees, dernier.NombreMisAJour, dernier.NombreErreurs,
                },
            });
        }).WithSummary("État du connecteur France VAE et portée réelle de son API.");

        g.MapPost("/france-vae/candidature/{identifiant}", async (
                string identifiant, FranceVaeClient client, VaeDbContext db, CancellationToken ct) =>
        {
            if (!client.EstConfigure)
            {
                return Results.Problem(
                    "Le connecteur France VAE n'est pas configuré : renseignez un jeton dans FranceVae:Jeton.",
                    statusCode: 503);
            }

            var candidature = await client.ObtenirCandidatureAsync(identifiant, ct);
            if (candidature is null)
            {
                return Results.NotFound(new { message = "Candidature introuvable côté France VAE." });
            }

            var parcours = await db.Parcours.Include(p => p.Candidat)
                .FirstOrDefaultAsync(p => p.CandidatureFranceVaeId == candidature.Id, ct);

            return Results.Ok(new
            {
                candidature.Id, candidature.Nom, candidature.Prenom, candidature.Email,
                candidature.CodeRncp, candidature.IntituleCertification, candidature.Statut,
                candidature.DateCandidature, candidature.Departement,
                dossierExistant = parcours?.Id,
            });
        }).WithSummary("Lit une candidature France VAE par son identifiant.");

        // ============================================================ import de fichier
        g.MapPost("/import/entetes", async (IFormFile fichier, CancellationToken ct) =>
        {
            await using var flux = fichier.OpenReadStream();
            using var memoire = new MemoryStream();
            await flux.CopyToAsync(memoire, ct);
            memoire.Position = 0;

            var entetes = ImportCandidaturesCsv.LireEntetes(memoire);
            memoire.Position = 0;

            return Results.Ok(new
            {
                entetes,
                mappagePropose = MappageColonnes.Deviner(entetes),
            });
        }).DisableAntiforgery()
          .WithSummary("Lit les en-têtes d'un fichier pour proposer la correspondance des colonnes.");

        g.MapPost("/import/candidatures", async (
                IFormFile fichier, [FromForm] bool simulation,
                [FromForm] string? mappage, ImportCandidaturesCsv importeur,
                ServiceAlertes alertes, ServiceNotifications notifications,
                GenerateurDossierCandidat generateur, ILoggerFactory journaux, CancellationToken ct) =>
        {
            MappageColonnes? colonnes = null;
            if (!string.IsNullOrWhiteSpace(mappage))
            {
                try
                {
                    colonnes = JsonSerializer.Deserialize<MappageColonnes>(mappage,
                        new JsonSerializerOptions(JsonSerializerDefaults.Web));
                }
                catch (JsonException ex)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["mappage"] = [$"Correspondance de colonnes illisible : {ex.Message}"],
                    });
                }
            }

            await using var flux = fichier.OpenReadStream();
            var resultat = await importeur.ImporterAsync(flux, colonnes, simulation, "manuel", ct);

            if (!simulation)
            {
                await alertes.RafraichirAsync(ct);
                await notifications.NotifierImportAsync(resultat.ParcoursCrees, "import France VAE", ct);
                foreach (var parcoursId in resultat.ParcoursCrees)
                {
                    await CandidatsEndpoints.GenererDossierAsync(generateur, parcoursId, journaux, ct);
                }
            }

            return Results.Ok(new
            {
                simulation,
                resultat.Lus, resultat.Crees, resultat.MisAJour, resultat.Ignores, resultat.Erreurs,
                journal = resultat.Journal,
            });
        }).DisableAntiforgery()
          .WithSummary("Importe un export de candidatures. La simulation ne modifie rien.");

        // ============================================================ site EI Groupe
        // Point d'entrée public appelé par le formulaire du site. Protégé par un
        // secret partagé plutôt que par une session : le site n'a pas de compte.
        app.MapPost("/api/public/candidature", async (
                DemandeSiteWeb demande, HttpRequest requete, VaeDbContext db,
                IOptions<SiteWebOptions> options, ILoggerFactory logs, CancellationToken ct) =>
        {
            var o = options.Value;
            if (!o.EstConfigure)
            {
                return Results.Problem("Le point d'entrée du site web n'est pas configuré.", statusCode: 503);
            }

            var secret = requete.Headers["X-EiVae-Secret"].ToString();
            if (!CryptographieConstante.Egal(secret, o.Secret!))
            {
                logs.CreateLogger("SiteWeb").LogWarning(
                    "Demande refusée : secret invalide (origine {Origine}).",
                    requete.HttpContext.Connection.RemoteIpAddress);
                return Results.Unauthorized();
            }

            if (string.IsNullOrWhiteSpace(demande.Nom) || string.IsNullOrWhiteSpace(demande.Prenom))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["nom"] = ["Le nom et le prénom sont obligatoires."],
                });
            }

            var chiffres = new string((demande.Certification ?? string.Empty).Where(char.IsDigit).ToArray());
            var codeDetecte = chiffres.Length >= 4 ? "RNCP" + chiffres : null;

            var entree = new DemandeWeb
            {
                Nom = demande.Nom.Trim().ToUpperInvariant(),
                Prenom = demande.Prenom.Trim(),
                Email = demande.Email?.Trim(),
                Telephone = demande.Telephone?.Trim(),
                CodePostal = demande.CodePostal?.Trim(),
                Ville = demande.Ville?.Trim(),
                CertificationSouhaitee = demande.Certification?.Trim(),
                CodeRncpDetecte = codeDetecte,
                Message = demande.Message?.Trim(),
                Source = demande.Source ?? o.UrlFormulaire,
                Statut = "Nouvelle",
                PayloadJson = JsonSerializer.Serialize(demande),
            };

            db.DemandesWeb.Add(entree);
            await db.SaveChangesAsync(ct);

            // La demande n'entre pas directement dans la base candidats : elle
            // attend une qualification humaine. C'est ce qui évite d'y déverser
            // les soumissions automatisées.
            return Results.Accepted($"/api/integrations/demandes-web/{entree.Id}",
                new { entree.Id, message = "Demande enregistrée, en attente de qualification." });
        }).AllowAnonymous()
          .WithTags("Intégrations")
          .WithSummary("Reçoit une candidature du formulaire du site groupe-ei.fr.");

        g.MapGet("/demandes-web", async (string? statut, VaeDbContext db, CancellationToken ct) =>
        {
            var q = db.DemandesWeb.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(statut))
            {
                q = q.Where(d => d.Statut == statut);
            }

            return Results.Ok(await q.OrderByDescending(d => d.RecueLe).Take(300)
                .Select(d => new
                {
                    d.Id, d.Nom, d.Prenom, d.Email, d.Telephone, d.Ville, d.CodePostal,
                    d.CertificationSouhaitee, d.CodeRncpDetecte, d.Message, d.Source,
                    d.RecueLe, d.Statut, d.ParcoursCreeId, d.CommentaireTraitement, d.TraiteeLe,
                }).ToListAsync(ct));
        }).WithSummary("File des demandes reçues du site, en attente de qualification.");

        g.MapPost("/demandes-web/{id:int}/qualifier", async (
                int id, QualifierDemande q, VaeDbContext db, ParametresService parametres,
                ServiceAlertes alertes, ServiceNotifications notifications,
                GenerateurDossierCandidat generateur, ILoggerFactory journaux, CancellationToken ct) =>
        {
            var d = await db.DemandesWeb.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (d is null)
            {
                return Results.NotFound();
            }

            if (d.Statut != "Nouvelle")
            {
                return Results.Conflict(new { message = $"Cette demande est déjà « {d.Statut} »." });
            }

            if (q.Rejeter)
            {
                d.Statut = "Rejetée";
                d.CommentaireTraitement = q.Commentaire;
                d.TraiteeLe = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync(ct);
                return Results.Ok(new { d.Id, d.Statut });
            }

            var certificationId = q.CertificationId;
            if (certificationId is null && d.CodeRncpDetecte is not null)
            {
                certificationId = await db.Certifications
                    .Where(c => c.CodeRncp == d.CodeRncpDetecte)
                    .Select(c => (int?)c.Id).FirstOrDefaultAsync(ct);
            }

            var aujourdHui = DateOnly.FromDateTime(DateTime.UtcNow);

            var candidat = new Candidat
            {
                Nom = d.Nom, Prenom = d.Prenom, Email = d.Email, Telephone = d.Telephone,
                Ville = d.Ville, CodePostal = d.CodePostal,
                Departement = d.CodePostal is { Length: >= 2 } cp ? cp[..2] : null,
            };

            var parcours = new Parcours
            {
                Candidat = candidat,
                CertificationId = certificationId,
                Origine = OrigineCandidature.SiteWeb,
                Etape = EtapeParcours.Qualification,
                DateDemande = DateOnly.FromDateTime(d.RecueLe.UtcDateTime),
                DateDernierMouvement = aujourdHui,
                AapId = q.AapId,
                Historique = d.Message,
            };

            var tarification = await parametres.TarificationAsync(ct);
            parcours.DateDebutParcours = parcours.CalculerDateDebut() ?? aujourdHui;
            parcours.GrilleTarifaireId = tarification.GrillePour(parcours.DateDebutParcours.Value).Id;

            db.Candidats.Add(candidat);
            db.Parcours.Add(parcours);

            d.Statut = "Qualifiée";
            d.CommentaireTraitement = q.Commentaire;
            d.TraiteeLe = DateTimeOffset.UtcNow;

            await db.SaveChangesAsync(ct);
            d.ParcoursCreeId = parcours.Id;
            await db.SaveChangesAsync(ct);
            await alertes.RafraichirAsync(parcours.Id, ct);
            var dossier = await CandidatsEndpoints.GenererDossierAsync(generateur, parcours.Id, journaux, ct);
            await notifications.NotifierAsync(parcours.Id, null, ct);

            return Results.Ok(new { d.Id, d.Statut, parcoursId = parcours.Id, dossier });
        }).WithSummary("Transforme une demande du site en dossier candidat, ou la rejette.");

        // ============================================================ journal
        g.MapGet("/imports", async (VaeDbContext db, CancellationToken ct) =>
            Results.Ok(await db.Imports.AsNoTracking()
                .OrderByDescending(i => i.DemarreLe).Take(60)
                .Select(i => new
                {
                    i.Id, source = i.Source.ToString(), statut = i.Statut.ToString(),
                    i.DemarreLe, i.TermineLe, i.Reference, i.Declencheur,
                    i.NombreLus, i.NombreCrees, i.NombreMisAJour, i.NombreIgnores, i.NombreErreurs,
                }).ToListAsync(ct)))
            .WithSummary("Journal des imports, toutes sources confondues.");
    }
}

/// <summary>Comparaison à temps constant, pour ne pas divulguer la longueur du secret.</summary>
internal static class CryptographieConstante
{
    public static bool Egal(string? a, string? b)
    {
        if (a is null || b is null)
        {
            return false;
        }

        var octetsA = System.Text.Encoding.UTF8.GetBytes(a);
        var octetsB = System.Text.Encoding.UTF8.GetBytes(b);
        return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(octetsA, octetsB);
    }
}
