using EiVae.Api.Services;
using EiVae.Infrastructure;
using EiVae.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EiVae.Api.Endpoints;

public static class NotificationsEndpoints
{
    public static void MapNotificationsEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/notifications").WithTags("Notifications");

        g.MapGet("/", async (string? destinataire, bool? nonLues, VaeDbContext db, CancellationToken ct) =>
        {
            var q = db.Notifications.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(destinataire))
            {
                q = q.Where(n => n.DestinataireNom == destinataire);
            }

            if (nonLues == true)
            {
                q = q.Where(n => n.LueLe == null);
            }

            var lignes = await q.OrderByDescending(n => n.CreeLe).Take(300)
                .Select(n => new
                {
                    n.Id, n.Evenement, n.Titre, n.Message, n.ParcoursId, n.DestinataireNom, n.DestinataireEmail,
                    n.CreeLe, n.LueLe, n.EmailEnvoyeLe, n.ErreurEmail, n.Lien, n.LibelleLien,
                    candidat = n.Parcours == null ? null : n.Parcours.Candidat!.Prenom + " " + n.Parcours.Candidat.Nom,
                }).ToListAsync(ct);

            return Results.Ok(lignes.Select(n => new
            {
                n.Id, n.Titre, n.Message, n.ParcoursId, n.DestinataireNom, n.DestinataireEmail,
                n.CreeLe, n.LueLe, n.EmailEnvoyeLe, n.ErreurEmail, n.candidat, n.Lien, n.LibelleLien,
                evenement = n.Evenement.ToString(),
                evenementLibelle = ServiceNotifications.LibelleEvenement(n.Evenement),
            }));
        }).WithSummary("Notifications émises, les plus récentes d'abord.");

        g.MapGet("/destinataires", async (VaeDbContext db, CancellationToken ct) =>
            Results.Ok(await db.Notifications.AsNoTracking()
                .GroupBy(n => n.DestinataireNom)
                .Select(x => new { nom = x.Key, total = x.Count(), nonLues = x.Count(n => n.LueLe == null) })
                .OrderBy(x => x.nom)
                .ToListAsync(ct)))
            .WithSummary("Destinataires ayant reçu au moins une notification.");

        g.MapGet("/etat", async (VaeDbContext db, IOptions<NotificationsOptions> options, CancellationToken ct) =>
            Results.Ok(new
            {
                nonLues = await db.Notifications.CountAsync(n => n.LueLe == null, ct),
                envoiCourriel = options.Value.Smtp.EstConfigure,
                enAttenteEnvoi = options.Value.Smtp.EstConfigure
                    ? await db.Notifications.CountAsync(n => n.DestinataireEmail != null && n.EmailEnvoyeLe == null
                                                             && n.TentativesEmail < EnvoiNotifications.TentativesMax, ct)
                    : 0,
            }))
            .WithSummary("Compteur de notifications non lues et état de l'envoi par courriel.");

        g.MapPost("/{id:long}/lue", async (long id, VaeDbContext db, CancellationToken ct) =>
        {
            var n = await db.Notifications.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (n is null)
            {
                return Results.NotFound();
            }

            n.LueLe ??= DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }).WithSummary("Marque une notification comme lue.");

        g.MapPost("/tout-lire", async (string? destinataire, VaeDbContext db, CancellationToken ct) =>
        {
            var maintenant = DateTimeOffset.UtcNow;
            var n = await db.Notifications
                .Where(x => x.LueLe == null && (destinataire == null || x.DestinataireNom == destinataire))
                .ExecuteUpdateAsync(x => x.SetProperty(y => y.LueLe, maintenant), ct);
            return Results.Ok(new { marquees = n });
        }).WithSummary("Marque comme lues toutes les notifications, ou celles d'un destinataire.");
    }
}
