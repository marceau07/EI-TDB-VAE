using EiVae.Domain;
using EiVae.Domain.Entities;
using EiVae.Domain.Services;
using EiVae.Infrastructure;
using EiVae.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace EiVae.Api.Endpoints;

/// <summary>Création ou mise à jour d'un intervenant depuis l'interface de saisie.</summary>
public sealed record SaisieIntervenant(
    string Nom,
    string? Prenom,
    TypeIntervenant Type,
    StatutIntervenant Statut,
    string? Email,
    string? Telephone,
    string? Territoire,
    string? Region,
    bool InterventionDistanciel,
    bool InterventionPresentiel,
    string? Specialites,
    decimal? TarifHoraire,
    int? CapaciteCandidats,
    decimal? CapaciteHeuresTrimestre,
    DateOnly? DateEntreeReseau,
    DateOnly? DateSortieReseau,
    string? Siret,
    string? Notes,
    int[]? CertificationsHabilitees);

public sealed record SaisieHabilitation(int CertificationId, string Niveau, DateOnly? DateHabilitation, string? Commentaire);

public static class IntervenantsEndpoints
{
    public static void MapIntervenantsEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/intervenants").WithTags("Réseau VAE");

        g.MapGet("/", async (
                TypeIntervenant? type, bool? mobilisables, VaeDbContext db,
                ParametresService parametres, SharePointLinkBuilder sharePoint, CancellationToken ct) =>
        {
            var capaciteDefaut = await parametres.EntierAsync("capacite.accompagnateur", 6, ct);

            var q = db.Intervenants
                .Include(i => i.Habilitations).ThenInclude(h => h.Certification)
                .AsQueryable();

            if (type is { } t)
            {
                q = q.Where(i => i.Type == t);
            }

            if (mobilisables is true)
            {
                q = q.Where(i => i.Statut == StatutIntervenant.Actif || i.Statut == StatutIntervenant.EnIntegration);
            }

            var intervenants = await q.AsSplitQuery().ToListAsync(ct);
            var ids = intervenants.Select(i => i.Id).ToList();

            // Charge courante : dossiers actifs par intervenant, tous rôles confondus.
            var chargeAap = await db.Parcours
                .Where(p => p.AapId != null && ids.Contains(p.AapId.Value)
                            && p.Etape != EtapeParcours.Cloture && p.Etape != EtapeParcours.Sortie)
                .GroupBy(p => p.AapId!.Value)
                .Select(x => new { Id = x.Key, N = x.Count() })
                .ToDictionaryAsync(x => x.Id, x => x.N, ct);

            var chargeAcc = await db.Parcours
                .Where(p => p.AccompagnateurId != null && ids.Contains(p.AccompagnateurId.Value)
                            && p.Etape != EtapeParcours.Cloture && p.Etape != EtapeParcours.Sortie)
                .GroupBy(p => p.AccompagnateurId!.Value)
                .Select(x => new { Id = x.Key, N = x.Count() })
                .ToDictionaryAsync(x => x.Id, x => x.N, ct);

            var heures = await db.Seances
                .Where(s => s.IntervenantId != null && ids.Contains(s.IntervenantId.Value) && s.Realisee)
                .GroupBy(s => s.IntervenantId!.Value)
                .Select(x => new { Id = x.Key, H = x.Sum(s => s.DureeHeures) })
                .ToDictionaryAsync(x => x.Id, x => x.H, ct);

            return Results.Ok(intervenants.Select(i =>
            {
                var actifs = (chargeAap.GetValueOrDefault(i.Id)) + (chargeAcc.GetValueOrDefault(i.Id));
                var capacite = i.CapaciteCandidats ?? capaciteDefaut;

                return new
                {
                    i.Id, i.Nom, i.Prenom, nomComplet = i.NomComplet,
                    type = i.Type.ToString(), typeLibelle = LibelleType(i.Type),
                    statut = i.Statut.ToString(), statutLibelle = LibelleStatut(i.Statut),
                    i.Email, i.Telephone, i.Territoire, i.Region,
                    i.InterventionDistanciel, i.InterventionPresentiel,
                    i.Specialites, i.TarifHoraire, i.Siret, i.Notes,
                    i.DateEntreeReseau, i.DateSortieReseau,
                    capacite,
                    candidatsActifs = actifs,
                    tauxCharge = capacite == 0 ? 0m : Math.Round((decimal)actifs / capacite, 3),
                    heuresRealisees = heures.GetValueOrDefault(i.Id),
                    mobilisable = i.EstMobilisable,
                    habilitations = i.Habilitations.Select(h => new
                    {
                        h.CertificationId, h.Niveau, h.DateHabilitation,
                        certification = h.Certification!.Abrege ?? h.Certification.Intitule,
                        h.Certification.CodeRncp,
                    }),
                    urlSharePoint = sharePoint.EstConfigure ? sharePoint.UrlDossierIntervenant(i) : null,
                };
            }));
        }).WithSummary("Liste le réseau avec la charge courante et les habilitations.");

        g.MapPost("/", async (SaisieIntervenant s, VaeDbContext db, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(s.Nom))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["nom"] = ["Le nom est obligatoire."],
                });
            }

            var existe = await db.Intervenants.AnyAsync(
                i => i.Nom.ToUpper() == s.Nom.ToUpper()
                     && (s.Prenom == null || i.Prenom!.ToUpper() == s.Prenom.ToUpper()), ct);

            var i = new Intervenant
            {
                Nom = s.Nom.Trim(),
                Prenom = Vide(s.Prenom),
                Type = s.Type,
                Statut = s.Statut,
                Email = Vide(s.Email),
                Telephone = Vide(s.Telephone),
                Territoire = Vide(s.Territoire),
                Region = Vide(s.Region),
                InterventionDistanciel = s.InterventionDistanciel,
                InterventionPresentiel = s.InterventionPresentiel,
                Specialites = Vide(s.Specialites),
                TarifHoraire = s.TarifHoraire,
                CapaciteCandidats = s.CapaciteCandidats,
                CapaciteHeuresTrimestre = s.CapaciteHeuresTrimestre,
                DateEntreeReseau = s.DateEntreeReseau ?? DateOnly.FromDateTime(DateTime.UtcNow),
                DateSortieReseau = s.DateSortieReseau,
                Siret = Vide(s.Siret),
                Notes = Vide(s.Notes),
            };

            foreach (var certificationId in s.CertificationsHabilitees ?? [])
            {
                i.Habilitations.Add(new HabilitationIntervenant
                {
                    CertificationId = certificationId,
                    Niveau = "Habilité",
                    DateHabilitation = DateOnly.FromDateTime(DateTime.UtcNow),
                });
            }

            db.Intervenants.Add(i);
            await db.SaveChangesAsync(ct);

            return Results.Created($"/api/intervenants/{i.Id}", new { i.Id, doublonPossible = existe });
        }).WithSummary("Ajoute un intervenant au réseau.");

        g.MapPut("/{id:int}", async (int id, SaisieIntervenant s, VaeDbContext db, CancellationToken ct) =>
        {
            var i = await db.Intervenants.Include(x => x.Habilitations)
                .FirstOrDefaultAsync(x => x.Id == id, ct);

            if (i is null)
            {
                return Results.NotFound();
            }

            i.Nom = s.Nom.Trim();
            i.Prenom = Vide(s.Prenom);
            i.Type = s.Type;
            i.Statut = s.Statut;
            i.Email = Vide(s.Email);
            i.Telephone = Vide(s.Telephone);
            i.Territoire = Vide(s.Territoire);
            i.Region = Vide(s.Region);
            i.InterventionDistanciel = s.InterventionDistanciel;
            i.InterventionPresentiel = s.InterventionPresentiel;
            i.Specialites = Vide(s.Specialites);
            i.TarifHoraire = s.TarifHoraire;
            i.CapaciteCandidats = s.CapaciteCandidats;
            i.CapaciteHeuresTrimestre = s.CapaciteHeuresTrimestre;
            i.DateEntreeReseau = s.DateEntreeReseau;
            i.DateSortieReseau = s.DateSortieReseau;
            i.Siret = Vide(s.Siret);
            i.Notes = Vide(s.Notes);

            if (s.CertificationsHabilitees is { } certifications)
            {
                var voulues = certifications.ToHashSet();
                var actuelles = i.Habilitations.Select(h => h.CertificationId).ToHashSet();

                foreach (var h in i.Habilitations.Where(h => !voulues.Contains(h.CertificationId)).ToList())
                {
                    i.Habilitations.Remove(h);
                    db.Habilitations.Remove(h);
                }

                foreach (var c in voulues.Where(c => !actuelles.Contains(c)))
                {
                    i.Habilitations.Add(new HabilitationIntervenant
                    {
                        CertificationId = c,
                        Niveau = "Habilité",
                        DateHabilitation = DateOnly.FromDateTime(DateTime.UtcNow),
                    });
                }
            }

            await db.SaveChangesAsync(ct);
            return Results.Ok(new { i.Id });
        }).WithSummary("Met à jour un intervenant et ses habilitations.");

        g.MapPost("/{id:int}/habilitations", async (
                int id, SaisieHabilitation h, VaeDbContext db, CancellationToken ct) =>
        {
            if (!await db.Intervenants.AnyAsync(i => i.Id == id, ct))
            {
                return Results.NotFound();
            }

            var existante = await db.Habilitations.FirstOrDefaultAsync(
                x => x.IntervenantId == id && x.CertificationId == h.CertificationId, ct);

            if (existante is not null)
            {
                existante.Niveau = h.Niveau;
                existante.DateHabilitation = h.DateHabilitation;
                existante.Commentaire = h.Commentaire;
            }
            else
            {
                db.Habilitations.Add(new HabilitationIntervenant
                {
                    IntervenantId = id,
                    CertificationId = h.CertificationId,
                    Niveau = h.Niveau,
                    DateHabilitation = h.DateHabilitation,
                    Commentaire = h.Commentaire,
                });
            }

            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }).WithSummary("Habilite un intervenant sur une certification.");

        // ------------------------------------------------------- aide à l'affectation
        g.MapGet("/proposer", async (
                int certificationId, string? territoire, bool? distanciel,
                VaeDbContext db, AffectationService affectation, ParametresService parametres,
                CancellationToken ct) =>
        {
            var certification = await db.Certifications.FirstOrDefaultAsync(c => c.Id == certificationId, ct);
            if (certification is null)
            {
                return Results.NotFound(new { message = "Certification inconnue." });
            }

            var capaciteDefaut = await parametres.EntierAsync("capacite.accompagnateur", 6, ct);

            var intervenants = await db.Intervenants
                .Include(i => i.Habilitations)
                .Where(i => i.Statut == StatutIntervenant.Actif || i.Statut == StatutIntervenant.EnIntegration)
                .ToListAsync(ct);

            var actifs = await db.Parcours
                .Where(p => p.AccompagnateurId != null
                            && p.Etape != EtapeParcours.Cloture && p.Etape != EtapeParcours.Sortie)
                .GroupBy(p => p.AccompagnateurId!.Value)
                .Select(x => new { Id = x.Key, N = x.Count() })
                .ToDictionaryAsync(x => x.Id, x => x.N, ct);

            var experience = await db.Parcours
                .Where(p => p.AccompagnateurId != null && p.CertificationId == certificationId)
                .GroupBy(p => p.AccompagnateurId!.Value)
                .Select(x => new { Id = x.Key, N = x.Count() })
                .ToDictionaryAsync(x => x.Id, x => x.N, ct);

            var charges = intervenants.ToDictionary(
                i => i.Id,
                i => new ChargeIntervenant(
                    i.Id, actifs.GetValueOrDefault(i.Id), 0, 0, i.CapaciteCandidats ?? capaciteDefaut));

            var propositions = affectation.Proposer(
                certification, intervenants, charges, experience, territoire, distanciel);

            return Results.Ok(propositions.Select(p => new
            {
                intervenantId = p.Intervenant.Id,
                nom = p.Intervenant.NomComplet,
                type = p.Intervenant.Type.ToString(),
                p.Score,
                motifs = p.Motifs,
                candidatsActifs = p.Charge.CandidatsActifs,
                capacite = p.Charge.Capacite,
                tauxCharge = p.Charge.TauxCharge,
                p.Intervenant.Territoire,
                p.Intervenant.Region,
                p.Intervenant.InterventionDistanciel,
            }));
        }).WithSummary("Classe les intervenants mobilisables pour une certification.");
    }

    private static string? Vide(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();

    public static string LibelleType(TypeIntervenant t) => t switch
    {
        TypeIntervenant.ArchitecteAccompagnateurParcours => "Architecte accompagnateur de parcours",
        TypeIntervenant.Accompagnateur => "Accompagnateur VAE",
        TypeIntervenant.ExpertMetier => "Expert métier",
        _ => "Fonction interne",
    };

    public static string LibelleStatut(StatutIntervenant s) => s switch
    {
        StatutIntervenant.Actif => "Actif",
        StatutIntervenant.EnIntegration => "En intégration",
        StatutIntervenant.Suspendu => "Suspendu",
        _ => "Retiré du réseau",
    };
}
