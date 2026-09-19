using EiVae.Domain;
using EiVae.Domain.Entities;
using EiVae.Domain.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EiVae.Infrastructure.Services;

/// <summary>
/// Réconcilie les alertes calculées avec celles déjà enregistrées.
///
/// Les alertes sont historisées plutôt que recalculées à la volée : on veut
/// pouvoir dire depuis combien de temps un dossier est bloqué, et non seulement
/// qu'il l'est aujourd'hui. Une alerte qui cesse d'être vraie est close, pas
/// supprimée.
/// </summary>
public sealed class ServiceAlertes(
    VaeDbContext db,
    ParametresService parametres,
    ILogger<ServiceAlertes> logger)
{
    /// <summary>Recalcule les alertes de tous les dossiers et met la base à jour.</summary>
    public Task<int> RafraichirAsync(CancellationToken ct = default) => RafraichirAsync(null, ct);

    /// <summary>Recalcule les alertes d'un dossier, ou de tous si <paramref name="parcoursId"/> est null.</summary>
    public async Task<int> RafraichirAsync(int? parcoursId, CancellationToken ct = default)
    {
        var moteur = await parametres.MoteurAsync(ct).ConfigureAwait(false);
        var aujourdHui = DateOnly.FromDateTime(DateTime.UtcNow);

        var parcours = await db.Parcours
            .Where(p => parcoursId == null || p.Id == parcoursId)
            .Include(p => p.Candidat)
            .Include(p => p.Certification)
            .Include(p => p.Financements)
            .Include(p => p.Factures)
            .Include(p => p.Seances)
            .Include(p => p.Alertes.Where(a => a.ResolueLe == null))
            .AsSplitQuery()
            .ToListAsync(ct).ConfigureAwait(false);

        var ouvertes = 0;

        foreach (var p in parcours)
        {
            var calculees = moteur.Evaluer(p, aujourdHui);
            var codesCalcules = calculees.Select(a => a.Regle.Code).ToHashSet(StringComparer.Ordinal);

            // Clôture des alertes qui ne se vérifient plus.
            foreach (var existante in p.Alertes.Where(a => a.EstOuverte && !codesCalcules.Contains(a.CodeRegle)))
            {
                existante.ResolueLe = DateTimeOffset.UtcNow;
            }

            // Ouverture des nouvelles ; mise à jour du détail des existantes.
            foreach (var a in calculees)
            {
                var existante = p.Alertes.FirstOrDefault(
                    x => x.EstOuverte && x.CodeRegle == a.Regle.Code);

                if (existante is null)
                {
                    db.Alertes.Add(new AlerteInstance
                    {
                        ParcoursId = p.Id,
                        CodeRegle = a.Regle.Code,
                        Severite = a.Regle.Severite,
                        Detail = a.Detail,
                    });
                }
                else
                {
                    existante.Detail = a.Detail;
                    existante.Severite = a.Regle.Severite;
                }

                ouvertes++;
            }
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        if (parcoursId is null)
        {
            logger.LogInformation("Moteur d'alertes : {Ouvertes} alertes actives sur {Dossiers} dossiers",
                ouvertes, parcours.Count);
        }

        return ouvertes;
    }

    /// <summary>Évalue un dossier sans écrire en base : utilisé pour l'affichage d'une fiche.</summary>
    public async Task<IReadOnlyList<AlerteCalculee>> EvaluerAsync(int parcoursId, CancellationToken ct = default)
    {
        var moteur = await parametres.MoteurAsync(ct).ConfigureAwait(false);

        var p = await db.Parcours
            .Include(x => x.Candidat)
            .Include(x => x.Certification)
            .Include(x => x.Financements)
            .Include(x => x.Factures)
            .Include(x => x.Seances)
            .FirstOrDefaultAsync(x => x.Id == parcoursId, ct).ConfigureAwait(false);

        return p is null ? [] : moteur.Evaluer(p, DateOnly.FromDateTime(DateTime.UtcNow));
    }

    /// <summary>Reporte une alerte : elle reste vraie mais sort du plan d'action.</summary>
    public async Task<bool> ReporterAsync(long alerteId, DateOnly jusquA, string? motif, CancellationToken ct = default)
    {
        var alerte = await db.Alertes.FirstOrDefaultAsync(a => a.Id == alerteId, ct).ConfigureAwait(false);
        if (alerte is null)
        {
            return false;
        }

        alerte.ReporteeJusquA = jusquA;
        alerte.MotifReport = motif;
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return true;
    }
}
