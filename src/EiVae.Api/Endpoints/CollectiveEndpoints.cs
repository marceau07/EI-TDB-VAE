using EiVae.Api.Services;
using EiVae.Domain;
using EiVae.Domain.Entities;
using EiVae.Domain.Services;
using EiVae.Infrastructure;
using EiVae.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace EiVae.Api.Endpoints;

public sealed record SaisieProjetCollectif(
    string Nom, string RaisonSociale, string? Siret, string? SecteurActivite, int? Effectif,
    string? ConventionCollective, string? Opco, string? ReferentNom, string? ReferentFonction,
    string? ReferentEmail, string? ReferentTelephone, int? AapReferentId, string Statut,
    DateOnly? DateDiagnostic, DateOnly? DateContractualisation, DateOnly? DateOuverture,
    DateOnly? DateCloturePrevue, DispositifFinancement Dispositif, decimal? MontantContractualise,
    string? ReferenceContrat, string? Commentaire);

public sealed record SaisieCohorte(
    string Nom, int? CertificationId, DateOnly? DateOuverture, DateOnly? DateCloturePrevue,
    int? EffectifCible, string? Rythme, int? AnimateurId, string? Commentaire);

public static class CollectiveEndpoints
{
    public static void MapCollectiveEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/collective").WithTags("VAE collective");

        g.MapGet("/projets", async (
                VaeDbContext db, ParametresService parametres, SharePointLinkBuilder sharePoint,
                CancellationToken ct) =>
        {
            var tarification = await parametres.TarificationAsync(ct);

            var projets = await db.ProjetsCollectifs
                .Include(p => p.AapReferent)
                .Include(p => p.Cohortes).ThenInclude(c => c.Certification)
                .Include(p => p.Cohortes).ThenInclude(c => c.Parcours)
                .AsSplitQuery().ToListAsync(ct);

            return Results.Ok(projets.Select(p => new
            {
                p.Id, p.Nom, p.RaisonSociale, p.Siret, p.SecteurActivite, p.Effectif,
                p.ConventionCollective, p.Opco, p.Statut,
                referent = new { p.ReferentNom, p.ReferentFonction, p.ReferentEmail, p.ReferentTelephone },
                aapReferent = p.AapReferent?.NomComplet,
                p.DateDiagnostic, p.DateContractualisation, p.DateOuverture, p.DateCloturePrevue, p.DateCloture,
                dispositif = ServiceTableauDeBord.LibelleDispositif(p.Dispositif),
                p.MontantContractualise, p.ReferenceContrat, p.Commentaire,
                urlSharePoint = sharePoint.EstConfigure ? sharePoint.UrlDossierProjet(p) : null,
                cohortes = p.Cohortes.Select(c => new
                {
                    c.Id, c.Nom, c.EffectifCible, c.Rythme, c.DateOuverture, c.DateCloturePrevue,
                    certification = c.Certification?.Abrege ?? c.Certification?.Intitule,
                    candidats = c.Parcours.Count,
                    actifs = c.Parcours.Count(x => x.Etape != EtapeParcours.Cloture
                                                   && x.Etape != EtapeParcours.Sortie),
                    caPrevisionnel = c.Parcours.Sum(x => tarification.Calculer(x).Total),
                    margePrevisionnelle = c.Parcours.Sum(x => tarification.Calculer(x).Marge),
                }),
            }));
        }).WithSummary("Projets d'entreprise, leurs cohortes et leur économie.");

        g.MapPost("/projets", async (SaisieProjetCollectif s, VaeDbContext db, CancellationToken ct) =>
        {
            var projet = new ProjetCollectif
            {
                Nom = s.Nom, RaisonSociale = s.RaisonSociale, Siret = s.Siret,
                SecteurActivite = s.SecteurActivite, Effectif = s.Effectif,
                ConventionCollective = s.ConventionCollective, Opco = s.Opco,
                ReferentNom = s.ReferentNom, ReferentFonction = s.ReferentFonction,
                ReferentEmail = s.ReferentEmail, ReferentTelephone = s.ReferentTelephone,
                AapReferentId = s.AapReferentId, Statut = s.Statut,
                DateDiagnostic = s.DateDiagnostic, DateContractualisation = s.DateContractualisation,
                DateOuverture = s.DateOuverture, DateCloturePrevue = s.DateCloturePrevue,
                Dispositif = s.Dispositif, MontantContractualise = s.MontantContractualise,
                ReferenceContrat = s.ReferenceContrat, Commentaire = s.Commentaire,
            };

            db.ProjetsCollectifs.Add(projet);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/collective/projets/{projet.Id}", new { projet.Id });
        }).WithSummary("Crée un projet de VAE collective.");

        g.MapPost("/projets/{id:int}/cohortes", async (
                int id, SaisieCohorte s, VaeDbContext db, CancellationToken ct) =>
        {
            if (!await db.ProjetsCollectifs.AnyAsync(p => p.Id == id, ct))
            {
                return Results.NotFound();
            }

            var cohorte = new Cohorte
            {
                ProjetCollectifId = id, Nom = s.Nom, CertificationId = s.CertificationId,
                DateOuverture = s.DateOuverture, DateCloturePrevue = s.DateCloturePrevue,
                EffectifCible = s.EffectifCible, Rythme = s.Rythme, AnimateurId = s.AnimateurId,
                Commentaire = s.Commentaire,
            };

            db.Cohortes.Add(cohorte);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/collective/cohortes/{cohorte.Id}", new { cohorte.Id });
        }).WithSummary("Ajoute une cohorte à un projet.");

        g.MapPost("/cohortes/{cohorteId:int}/rattacher/{parcoursId:int}", async (
                int cohorteId, int parcoursId, VaeDbContext db, CancellationToken ct) =>
        {
            var parcours = await db.Parcours.FirstOrDefaultAsync(p => p.Id == parcoursId, ct);
            if (parcours is null || !await db.Cohortes.AnyAsync(c => c.Id == cohorteId, ct))
            {
                return Results.NotFound();
            }

            parcours.CohorteId = cohorteId;
            parcours.Origine = OrigineCandidature.VaeCollective;
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }).WithSummary("Rattache un dossier à une cohorte. Le suivi individuel reste entier.");

        g.MapGet("/cohortes/{id:int}", async (
                int id, VaeDbContext db, ParametresService parametres, CancellationToken ct) =>
        {
            var tarification = await parametres.TarificationAsync(ct);

            var c = await db.Cohortes
                .Include(x => x.ProjetCollectif)
                .Include(x => x.Certification)
                .Include(x => x.Animateur)
                .Include(x => x.Ateliers).ThenInclude(a => a.Intervenant)
                .Include(x => x.Parcours).ThenInclude(p => p.Candidat)
                .Include(x => x.Parcours).ThenInclude(p => p.Alertes.Where(a => a.ResolueLe == null))
                .AsSplitQuery().FirstOrDefaultAsync(x => x.Id == id, ct);

            if (c is null)
            {
                return Results.NotFound();
            }

            // L'avancement collectif ne doit jamais masquer un décrochage individuel :
            // on montre l'écart de chaque candidat à l'étape médiane de sa cohorte.
            var etapes = c.Parcours.Select(p => (int)p.Etape).OrderBy(x => x).ToList();
            var etapeMediane = etapes.Count > 0 ? etapes[etapes.Count / 2] : 0;

            return Results.Ok(new
            {
                c.Id, c.Nom, c.Rythme, c.EffectifCible, c.DateOuverture, c.DateCloturePrevue,
                projet = new { c.ProjetCollectif!.Id, c.ProjetCollectif.Nom, c.ProjetCollectif.RaisonSociale },
                certification = c.Certification?.Abrege ?? c.Certification?.Intitule,
                animateur = c.Animateur?.NomComplet,
                economie = new
                {
                    caPrevisionnel = c.Parcours.Sum(p => tarification.Calculer(p).Total),
                    margePrevisionnelle = c.Parcours.Sum(p => tarification.Calculer(p).Marge),
                    heuresCollectives = c.Ateliers.Sum(a => a.DureeHeures),
                },
                ateliers = c.Ateliers.OrderBy(a => a.Date).Select(a => new
                {
                    a.Id, a.Theme, a.Date, a.DureeHeures, a.Modalite, a.Realise,
                    intervenant = a.Intervenant?.NomComplet,
                }),
                candidats = c.Parcours.Select(p => new
                {
                    p.Id,
                    nom = $"{p.Candidat?.Prenom} {p.Candidat?.Nom}".Trim(),
                    etape = MoteurAlertes.Libelle(p.Etape),
                    rang = (int)p.Etape,
                    ecartCohorte = (int)p.Etape - etapeMediane,
                    enRetard = (int)p.Etape < etapeMediane,
                    alertes = p.Alertes.Count(a => a.EstOuverte),
                }).OrderBy(x => x.rang),
            });
        }).WithSummary("Suivi d'une cohorte, avec l'écart de chaque candidat à l'avancement médian.");
    }
}
