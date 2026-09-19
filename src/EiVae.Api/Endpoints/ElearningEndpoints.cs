using EiVae.Domain;
using EiVae.Domain.Entities;
using EiVae.Domain.Services;
using EiVae.Infrastructure;
using EiVae.Infrastructure.Documents;
using Microsoft.EntityFrameworkCore;

namespace EiVae.Api.Endpoints;

public sealed record SaisieModule(
    string? Code, string? Titre, NatureModule Nature, string? Type, decimal? DureeHeures,
    string? Url, string? Description, bool Actif,
    List<int>? Certifications = null, List<int>? CertificationsObligatoires = null);

public sealed record SuiviModule(int ModuleId, StatutModule Statut);

public sealed record SaisieSuiviElearning(
    bool EspaceAcademieCree, string? UrlEspaceAcademie, DateOnly? DateDerniereActivite,
    List<SuiviModule>? Modules = null);

/// <summary>
/// Espace e-learning : catalogue EI Académie (modules e-learning et compléments
/// formatifs), rattachement aux certifications, et suivi des modules inclus dans
/// les parcours.
/// </summary>
public static class ElearningEndpoints
{
    public static void MapElearningEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/elearning").WithTags("E-learning");

        // ------------------------------------------------------------ catalogue
        g.MapGet("/modules", async (NatureModule? nature, bool? actifsSeulement, VaeDbContext db, CancellationToken ct) =>
        {
            var q = db.ModulesAcademie.AsNoTracking()
                .Include(m => m.Certifications).ThenInclude(c => c.Certification)
                .Include(m => m.Parcours).ThenInclude(p => p.Parcours)
                .AsSplitQuery()
                .AsQueryable();

            if (nature is { } n)
            {
                q = q.Where(m => m.Nature == n);
            }

            if (actifsSeulement == true)
            {
                q = q.Where(m => m.Actif);
            }

            var modules = await q.OrderBy(m => m.Nature).ThenBy(m => m.Type).ThenBy(m => m.Titre).ToListAsync(ct);

            return Results.Ok(modules.Select(m => new
            {
                m.Id, m.Code, m.Titre, m.Nature, m.Type, m.DureeHeures, m.Url, m.Description, m.Actif,
                certifications = m.Certifications
                    .OrderBy(c => c.Certification!.Abrege ?? c.Certification.Intitule)
                    .Select(c => new
                    {
                        id = c.CertificationId,
                        libelle = c.Certification!.Abrege ?? c.Certification.Intitule,
                        c.Obligatoire,
                    }),
                parcours = m.Parcours.Count,
                parcoursActifs = m.Parcours.Count(p => p.Parcours!.EstActif),
                termines = m.Parcours.Count(p => p.Statut == StatutModule.Termine),
            }));
        }).WithSummary("Catalogue EI Académie : modules e-learning et compléments formatifs.");

        g.MapPost("/modules", async (SaisieModule s, VaeDbContext db, CancellationToken ct) =>
        {
            if (Verifier(s) is { } erreurs)
            {
                return Results.ValidationProblem(erreurs);
            }

            var code = s.Code!.Trim().ToUpperInvariant();
            if (await db.ModulesAcademie.AnyAsync(m => m.Code == code, ct))
            {
                return Results.Conflict(new { message = $"Le code {code} est déjà utilisé." });
            }

            var m = new ModuleAcademie { Code = code, Titre = string.Empty, Type = string.Empty };
            Appliquer(m, s);
            db.ModulesAcademie.Add(m);
            await db.SaveChangesAsync(ct);
            await RattacherAsync(db, m.Id, s, ct);
            return Results.Created($"/api/elearning/modules/{m.Id}", new { m.Id, m.Code });
        }).WithSummary("Ajoute un module au catalogue.");

        g.MapPut("/modules/{id:int}", async (int id, SaisieModule s, VaeDbContext db, CancellationToken ct) =>
        {
            var m = await db.ModulesAcademie.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (m is null)
            {
                return Results.NotFound();
            }

            if (Verifier(s) is { } erreurs)
            {
                return Results.ValidationProblem(erreurs);
            }

            var code = s.Code!.Trim().ToUpperInvariant();
            if (await db.ModulesAcademie.AnyAsync(x => x.Id != id && x.Code == code, ct))
            {
                return Results.Conflict(new { message = $"Le code {code} est déjà utilisé." });
            }

            m.Code = code;
            Appliquer(m, s);
            await db.SaveChangesAsync(ct);
            await RattacherAsync(db, m.Id, s, ct);
            return Results.Ok(new { m.Id, m.Code });
        }).WithSummary("Modifie un module du catalogue.");

        g.MapDelete("/modules/{id:int}", async (int id, VaeDbContext db, CancellationToken ct) =>
        {
            var m = await db.ModulesAcademie.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (m is null)
            {
                return Results.NotFound();
            }

            var usages = await db.ParcoursModules.CountAsync(x => x.ModuleAcademieId == id, ct);
            if (usages > 0)
            {
                return Results.Conflict(new
                {
                    message = $"Ce module figure dans {usages} parcours : désactivez-le plutôt que de le supprimer.",
                });
            }

            db.ModulesAcademie.Remove(m);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }).WithSummary("Supprime un module jamais prescrit.");

        // ------------------------------------------------------------ suivi des parcours
        g.MapGet("/suivi", async (VaeDbContext db, CancellationToken ct) =>
        {
            // Dossiers concernés : ceux qui ont des modules, et ceux dont la
            // recevabilité est validée — c'est à ce moment que l'espace s'ouvre.
            var parcours = await db.Parcours.AsNoTracking()
                .Include(p => p.Candidat)
                .Include(p => p.Certification)
                .Include(p => p.Accompagnateur)
                .Include(p => p.Modules).ThenInclude(x => x.ModuleAcademie)
                .Where(p => p.Etape != EtapeParcours.Sortie && p.Etape != EtapeParcours.Cloture)
                .Where(p => p.Modules.Any() || p.Etape >= EtapeParcours.Recevabilite || p.DateRecevabilite != null)
                .AsSplitQuery()
                .ToListAsync(ct);

            return Results.Ok(parcours
                .OrderBy(p => p.EspaceAcademieCree).ThenBy(p => p.Candidat!.Nom)
                .Select(p => new
                {
                    p.Id,
                    candidat = $"{p.Candidat!.Prenom} {p.Candidat.Nom}",
                    p.Candidat.Email,
                    certification = p.Certification?.Abrege ?? p.Certification?.Intitule,
                    etape = MoteurAlertes.Libelle(p.Etape),
                    etapeRang = (int)p.Etape,
                    accompagnateur = p.Accompagnateur?.NomComplet,
                    p.EspaceAcademieCree, p.UrlEspaceAcademie, p.DateDerniereActiviteAcademie,
                    heuresComplement = p.HeuresComplementFormatifPrescrites,
                    modules = p.Modules.OrderBy(x => x.ModuleAcademie!.Nature).ThenBy(x => x.ModuleAcademie!.Titre)
                        .Select(x => new
                        {
                            id = x.ModuleAcademieId, x.ModuleAcademie!.Titre, x.ModuleAcademie.Code,
                            x.ModuleAcademie.Nature, x.ModuleAcademie.DureeHeures,
                            x.Statut, x.DateAttribution, x.DateFin,
                        }),
                }));
        }).WithSummary("Dossiers à suivre : espace candidat et avancement des modules.");

        g.MapPut("/parcours/{id:int}", async (
                int id, SaisieSuiviElearning s, VaeDbContext db, GenerateurDossierCandidat generateur,
                CancellationToken ct) =>
        {
            var p = await db.Parcours.Include(x => x.Modules).FirstOrDefaultAsync(x => x.Id == id, ct);
            if (p is null)
            {
                return Results.NotFound();
            }

            p.EspaceAcademieCree = s.EspaceAcademieCree;
            p.UrlEspaceAcademie = string.IsNullOrWhiteSpace(s.UrlEspaceAcademie) ? null : s.UrlEspaceAcademie.Trim();
            p.DateDerniereActiviteAcademie = s.DateDerniereActivite;

            var aujourdHui = DateOnly.FromDateTime(DateTime.UtcNow);
            foreach (var suivi in s.Modules ?? [])
            {
                if (p.Modules.FirstOrDefault(x => x.ModuleAcademieId == suivi.ModuleId) is not { } pm
                    || pm.Statut == suivi.Statut)
                {
                    continue;
                }

                pm.Statut = suivi.Statut;
                pm.DateFin = suivi.Statut is StatutModule.Termine or StatutModule.Abandonne ? aujourdHui : null;
                p.DateDerniereActiviteAcademie ??= aujourdHui;
            }

            await db.SaveChangesAsync(ct);
            await generateur.MettreAJourAsync(id, ct);
            return Results.Ok(new { p.Id });
        }).WithSummary("Met à jour l'espace candidat et l'avancement des modules d'un dossier.");
    }

    private static Dictionary<string, string[]>? Verifier(SaisieModule s)
    {
        var erreurs = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(s.Code) || s.Code.Trim().Length > 40)
        {
            erreurs["code"] = ["Code obligatoire, 40 caractères au plus."];
        }

        if (string.IsNullOrWhiteSpace(s.Titre))
        {
            erreurs["titre"] = ["Le titre est obligatoire."];
        }

        if (s.DureeHeures is < 0)
        {
            erreurs["dureeHeures"] = ["La durée ne peut pas être négative."];
        }

        return erreurs.Count > 0 ? erreurs : null;
    }

    private static void Appliquer(ModuleAcademie m, SaisieModule s)
    {
        m.Titre = s.Titre!.Trim();
        m.Nature = s.Nature;
        m.Type = string.IsNullOrWhiteSpace(s.Type)
            ? (s.Nature == NatureModule.ComplementFormatif ? "Complément formatif" : "Transversal")
            : s.Type.Trim();
        m.DureeHeures = s.DureeHeures;
        m.Url = string.IsNullOrWhiteSpace(s.Url) ? null : s.Url.Trim();
        m.Description = string.IsNullOrWhiteSpace(s.Description) ? null : s.Description.Trim();
        m.Actif = s.Actif;
    }

    /// <summary>Remplace les certifications rattachées au module, si la saisie les précise.</summary>
    private static async Task RattacherAsync(VaeDbContext db, int moduleId, SaisieModule s, CancellationToken ct)
    {
        if (s.Certifications is null)
        {
            return;
        }

        var existants = await db.CertificationModules.Where(x => x.ModuleAcademieId == moduleId).ToListAsync(ct);
        db.CertificationModules.RemoveRange(existants);

        var valides = await db.Certifications.Where(c => s.Certifications.Contains(c.Id))
            .Select(c => c.Id).ToListAsync(ct);
        var obligatoires = s.CertificationsObligatoires ?? [];

        db.CertificationModules.AddRange(valides.Select(cid => new CertificationModule
        {
            CertificationId = cid, ModuleAcademieId = moduleId, Obligatoire = obligatoires.Contains(cid),
        }));
        await db.SaveChangesAsync(ct);
    }
}
