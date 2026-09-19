using EiVae.Domain;
using EiVae.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EiVae.Infrastructure.Seeding;

/// <summary>
/// Charge les grilles tarifaires indispensables au démarrage. Idempotent — il
/// complète ce qui manque et ne réécrit jamais une valeur ajustée par le service.
///
/// Les paramètres de gestion, règles d'alerte et rôles sont créés une seule fois
/// par la migration qui les introduit : ils sont modifiables et supprimables
/// depuis l'application, et ne doivent pas réapparaître au redémarrage.
/// </summary>
public sealed class ReferentielSeeder(VaeDbContext db, ILogger<ReferentielSeeder> logger)
{
    /// <summary>
    /// Date d'entrée en vigueur de la grille 2026. Les parcours démarrés à partir
    /// de ce jour sont facturés au nouveau tarif ; les précédents restent à la
    /// grille 2025 jusqu'à leur clôture.
    /// </summary>
    public static readonly DateOnly EntreeEnVigueurGrille2026 = new(2026, 6, 15);

    public async Task AmorcerAsync(CancellationToken ct = default)
    {
        await GrillesAsync(ct).ConfigureAwait(false);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    private async Task GrillesAsync(CancellationToken ct)
    {
        var existantes = await db.GrillesTarifaires.Select(g => g.Code).ToListAsync(ct).ConfigureAwait(false);

        var grilles = new List<GrilleTarifaire>
        {
            new()
            {
                Code = "2025",
                Libelle = "Grille en vigueur jusqu'au 14 juin 2026",
                DateEffet = new DateOnly(2024, 1, 1),
                DateFin = EntreeEnVigueurGrille2026.AddDays(-1),
                ForfaitArchitecture = 300m,
                TarifHoraireIndividuel = 70m,
                TarifHoraireCollectif = 35m,
                TarifHoraireComplementFormatif = 25m,
                FraisJury = 350m,
                PlafondHeuresIndividuel = 30m,
                PlafondHeuresCollectif = 20m,
                PlafondHeuresComplementFormatif = 70m,
                PlafondMontantTotal = 5850m,
                CoutHoraireIntervenant = 30m,
                HeuresArchitectureParDossier = 3m,
                CoutHoraireArchitecte = 35m,
            },
            new()
            {
                Code = "2026",
                Libelle = "Grille applicable aux parcours démarrés à partir du 15 juin 2026",
                DateEffet = EntreeEnVigueurGrille2026,
                DateFin = null,
                ForfaitArchitecture = 350m,
                TarifHoraireIndividuel = 75m,
                TarifHoraireCollectif = 40m,
                TarifHoraireComplementFormatif = 30m,
                FraisJury = 350m,
                PlafondHeuresIndividuel = 30m,
                PlafondHeuresCollectif = 20m,
                PlafondHeuresComplementFormatif = 70m,
                PlafondMontantTotal = 5850m,
                CoutHoraireIntervenant = 30m,
                HeuresArchitectureParDossier = 3m,
                CoutHoraireArchitecte = 35m,
            },
        };

        foreach (var g in grilles.Where(g => !existantes.Contains(g.Code)))
        {
            db.GrillesTarifaires.Add(g);
            logger.LogInformation("Grille tarifaire « {Code} » chargée, effet au {Date:dd/MM/yyyy}",
                g.Code, g.DateEffet);
        }
    }
}
