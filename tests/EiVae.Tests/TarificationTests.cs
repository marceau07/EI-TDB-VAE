using EiVae.Domain.Entities;
using EiVae.Domain.Services;

namespace EiVae.Tests;

/// <summary>
/// La règle tarifaire est la plus coûteuse à corriger après coup : une erreur
/// de grille se retrouve dans un devis signé. Ces tests la verrouillent.
/// </summary>
public sealed class TarificationTests
{
    private static readonly DateOnly EntreeEnVigueur2026 = new(2026, 6, 15);

    private static TarificationService Service() => new(
    [
        new GrilleTarifaire
        {
            Id = 1, Code = "2025", Libelle = "Grille 2025",
            DateEffet = new DateOnly(2024, 1, 1), DateFin = EntreeEnVigueur2026.AddDays(-1),
            ForfaitArchitecture = 300m, TarifHoraireIndividuel = 70m, TarifHoraireCollectif = 35m,
            TarifHoraireComplementFormatif = 25m, FraisJury = 350m,
            PlafondHeuresIndividuel = 30m, PlafondHeuresCollectif = 20m,
            PlafondHeuresComplementFormatif = 70m, PlafondMontantTotal = 5850m,
            CoutHoraireIntervenant = 30m, HeuresArchitectureParDossier = 3m, CoutHoraireArchitecte = 35m,
        },
        new GrilleTarifaire
        {
            Id = 2, Code = "2026", Libelle = "Grille 2026",
            DateEffet = EntreeEnVigueur2026, DateFin = null,
            ForfaitArchitecture = 350m, TarifHoraireIndividuel = 75m, TarifHoraireCollectif = 40m,
            TarifHoraireComplementFormatif = 30m, FraisJury = 350m,
            PlafondHeuresIndividuel = 30m, PlafondHeuresCollectif = 20m,
            PlafondHeuresComplementFormatif = 70m, PlafondMontantTotal = 5850m,
            CoutHoraireIntervenant = 30m, HeuresArchitectureParDossier = 3m, CoutHoraireArchitecte = 35m,
        },
    ]);

    [Theory]
    [InlineData(2026, 6, 13, "2025")]
    [InlineData(2026, 6, 14, "2025")] // dernier jour de l'ancienne grille
    [InlineData(2026, 6, 15, "2026")] // entrée en vigueur, incluse
    [InlineData(2026, 6, 16, "2026")]
    [InlineData(2027, 1, 1, "2026")]
    public void La_grille_depend_du_jour_de_demarrage(int a, int m, int j, string attendu)
    {
        var grille = Service().GrillePour(new DateOnly(a, m, j));
        Assert.Equal(attendu, grille.Code);
    }

    [Fact]
    public void Un_parcours_anterieur_reste_a_la_grille_2025()
    {
        var parcours = new Parcours
        {
            DateDebutParcours = new DateOnly(2026, 3, 1),
            HeuresIndividuelPrescrites = 20m,
            HeuresCollectifPrescrites = 2m,
            ForfaitArchitectureApplique = true,
            FraisJuryInclus = true,
        };

        var calcul = Service().Calculer(parcours);

        Assert.Equal("2025", calcul.CodeGrille);
        // 300 forfait + 350 jury + 20 × 70 + 2 × 35 = 2 120 €
        Assert.Equal(2120m, calcul.Total);
    }

    [Fact]
    public void Le_parcours_standard_2026_vaut_bien_2280_euros()
    {
        // Valeur de référence publiée dans la plaquette tarifaire EI Groupe :
        // parcours standard = 2 280 €. Toute dérive ici est une régression.
        var parcours = new Parcours
        {
            DateDebutParcours = new DateOnly(2026, 9, 1),
            HeuresIndividuelPrescrites = 20m, // 12 h synchrone + 8 h asynchrone
            HeuresCollectifPrescrites = 2m,
            ForfaitArchitectureApplique = true,
            FraisJuryInclus = true,
        };

        var calcul = Service().Calculer(parcours);

        Assert.Equal("2026", calcul.CodeGrille);
        Assert.Equal(2280m, calcul.Total);
    }

    [Theory]
    [InlineData(24, 4, 10, 2960)]  // parcours renforcé
    [InlineData(36, 4, 21, 4190)]  // parcours intensif
    public void Les_parcours_de_reference_2026_correspondent_a_la_plaquette(
        decimal individuel, decimal collectif, decimal complement, decimal attendu)
    {
        var calcul = Service().Calculer(new Parcours
        {
            DateDebutParcours = new DateOnly(2026, 9, 1),
            HeuresIndividuelPrescrites = individuel,
            HeuresCollectifPrescrites = collectif,
            HeuresComplementFormatifPrescrites = complement,
            ForfaitArchitectureApplique = true,
            FraisJuryInclus = true,
        });

        Assert.Equal(attendu, calcul.Total);
    }

    [Fact]
    public void Une_grille_figee_sur_le_dossier_prime_sur_la_date()
    {
        // Cas réel : le devis a été signé à l'ancienne grille, mais la date de
        // démarrage a été corrigée après coup. Le devis fait foi.
        var service = Service();
        var grille2025 = service.GrillePour(new DateOnly(2025, 1, 1));

        var parcours = new Parcours
        {
            DateDebutParcours = new DateOnly(2026, 12, 1),
            GrilleTarifaire = grille2025,
            HeuresIndividuelPrescrites = 10m,
            ForfaitArchitectureApplique = true,
        };

        Assert.Equal("2025", service.Calculer(parcours).CodeGrille);
    }

    [Fact]
    public void Les_plafonds_sont_signales_sans_etre_appliques_d_office()
    {
        // Le montant doit refléter les heures réellement prescrites : c'est au
        // service d'arbitrer, pas au calcul de tronquer silencieusement.
        var calcul = Service().Calculer(new Parcours
        {
            DateDebutParcours = new DateOnly(2026, 9, 1),
            HeuresIndividuelPrescrites = 40m, // plafond : 30 h
            ForfaitArchitectureApplique = true,
        });

        Assert.False(calcul.PlafondRespecte);
        Assert.Contains(calcul.Depassements, d => d.Contains("individuel", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(350m + 40m * 75m, calcul.Total);
    }

    [Fact]
    public void Une_heure_collective_est_facturee_par_participant_mais_animee_une_fois()
    {
        // C'est la mécanique économique de la VAE collective : le chiffre
        // d'affaires suit le nombre de participants, le coût ne suit pas.
        var service = Service();
        var grille = service.GrillePour(new DateOnly(2026, 9, 1));

        var seul = service.Calculer(grille, false, 0m, 10m, 0m, false, 1);
        var trois = service.Calculer(grille, false, 0m, 10m, 0m, false, 3);

        Assert.Equal(seul.Total * 3, trois.Total);
        Assert.Equal(seul.CoutPedagogique, trois.CoutPedagogique);
        Assert.True(trois.TauxMarge > seul.TauxMarge);
    }

    [Fact]
    public void Le_ca_par_heure_mobilisee_est_superieur_en_cohorte()
    {
        var service = Service();
        var grille = service.GrillePour(new DateOnly(2026, 9, 1));

        // Cohorte : 20 h collectives mutualisées + 6 h individuelles par candidate.
        var cohorte = service.CaParHeureMobilisee(grille, true, 6m, 20m, 8m, true, 3);
        // Individuel équivalent : tout en face-à-face.
        var individuel = service.CaParHeureMobilisee(grille, true, 20m, 2m, 0m, true, 1);

        Assert.True(cohorte > individuel,
            $"La cohorte devrait produire davantage par heure mobilisée : {cohorte} contre {individuel}.");
    }

    [Fact]
    public void Une_date_anterieure_a_toute_grille_retombe_sur_la_plus_ancienne()
    {
        // Les dossiers historiques doivent rester calculables, pas lever d'exception.
        Assert.Equal("2025", Service().GrillePour(new DateOnly(2019, 1, 1)).Code);
    }

    [Fact]
    public void Le_service_refuse_de_demarrer_sans_grille()
    {
        Assert.Throws<ArgumentException>(() => new TarificationService([]));
    }
}
