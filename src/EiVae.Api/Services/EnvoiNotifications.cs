using System.Net;
using System.Net.Mail;
using EiVae.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EiVae.Api.Services;

public sealed class NotificationsOptions
{
    public const string Section = "Notifications";

    /// <summary>Adresse publique de l'application, pour le lien placé dans les courriels.</summary>
    public string UrlApplication { get; set; } = string.Empty;

    public SmtpOptions Smtp { get; set; } = new();
}

public sealed class SmtpOptions
{
    public string Hote { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public bool Ssl { get; set; } = true;
    public string? Utilisateur { get; set; }
    public string? MotDePasse { get; set; }
    public string Expediteur { get; set; } = string.Empty;
    public string NomExpediteur { get; set; } = "Pilotage VAE — EI Groupe";

    public bool EstConfigure => !string.IsNullOrWhiteSpace(Hote) && !string.IsNullOrWhiteSpace(Expediteur);
}

/// <summary>
/// Envoie par courriel les notifications adressées à une personne. Sans serveur
/// SMTP configuré, le service ne fait rien : les notifications restent
/// consultables dans l'application.
/// </summary>
public sealed class EnvoiNotifications(
    IServiceScopeFactory portees,
    IOptions<NotificationsOptions> options,
    ILogger<EnvoiNotifications> logger) : BackgroundService
{
    public const int TentativesMax = 3;
    private static readonly TimeSpan Intervalle = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Smtp.EstConfigure)
        {
            logger.LogInformation("Envoi des notifications par courriel désactivé : aucun serveur SMTP configuré.");
            return;
        }

        using var minuterie = new PeriodicTimer(Intervalle);
        do
        {
            try
            {
                await EnvoyerEnAttenteAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Envoi des notifications interrompu");
            }
        }
        while (await minuterie.WaitForNextTickAsync(stoppingToken));
    }

    private async Task EnvoyerEnAttenteAsync(CancellationToken ct)
    {
        await using var portee = portees.CreateAsyncScope();
        var db = portee.ServiceProvider.GetRequiredService<VaeDbContext>();

        var enAttente = await db.Notifications
            .Where(n => n.DestinataireEmail != null && n.EmailEnvoyeLe == null && n.TentativesEmail < TentativesMax)
            .OrderBy(n => n.CreeLe).Take(50)
            .ToListAsync(ct);

        if (enAttente.Count == 0)
        {
            return;
        }

        var smtp = options.Value.Smtp;
        using var client = new SmtpClient(smtp.Hote, smtp.Port) { EnableSsl = smtp.Ssl };
        if (!string.IsNullOrWhiteSpace(smtp.Utilisateur))
        {
            client.Credentials = new NetworkCredential(smtp.Utilisateur, smtp.MotDePasse);
        }

        var lien = options.Value.UrlApplication.TrimEnd('/');

        foreach (var n in enAttente)
        {
            n.TentativesEmail++;
            try
            {
                using var message = new MailMessage(
                    new MailAddress(smtp.Expediteur, smtp.NomExpediteur),
                    new MailAddress(n.DestinataireEmail!, n.DestinataireNom))
                {
                    Subject = n.Titre,
                    Body = n.Message
                           + LienAbsolu(n.Lien, n.LibelleLien, lien)
                           + (lien.Length > 0 && n.ParcoursId is { } id ? $"\n\nOuvrir le dossier : {lien}/#fiche-{id}" : string.Empty)
                           + "\n\n— Pilotage VAE, EI Groupe",
                };

                await client.SendMailAsync(message, ct);
                n.EmailEnvoyeLe = DateTimeOffset.UtcNow;
                n.ErreurEmail = null;
            }
            catch (Exception ex) when (ex is SmtpException or FormatException or InvalidOperationException)
            {
                n.ErreurEmail = ex.Message.Length > 1000 ? ex.Message[..1000] : ex.Message;
                logger.LogWarning(ex, "Notification {Id} : envoi à {Destinataire} impossible", n.Id, n.DestinataireNom);
            }
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>Un lien relatif n'a de sens dans un courriel que préfixé de l'adresse de l'application.</summary>
    private static string LienAbsolu(string? lien, string? libelle, string urlApplication)
    {
        if (string.IsNullOrWhiteSpace(lien) || lien.StartsWith("/#", StringComparison.Ordinal))
        {
            return string.Empty;
        }

        if (lien.StartsWith('/'))
        {
            if (urlApplication.Length == 0)
            {
                return string.Empty;
            }

            lien = urlApplication + lien;
        }

        return $"\n\n{libelle ?? "Lien"} : {lien}";
    }
}
