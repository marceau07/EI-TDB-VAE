using EiVae.Domain;
using EiVae.Domain.Entities;
using EiVae.Domain.Services;

namespace EiVae.Tests;

public sealed class MoteurAlertesTests
{
    private static readonly DateOnly Aujourdhui = new(2026, 8, 20);

    private static MoteurAlertes Moteur(IEnumerable<RegleAlerte>? regles = null) => new(
        regles ?? MoteurAlertes.ReglesParDefaut(),
        new TarificationService([new GrilleTarifaire
        {
            Id = 1, Code = "2026", Libelle = "Grille 2026",
            DateEffet = new DateOnly(2026, 6, 15),
            ForfaitArchitecture = 350m, TarifHoraireIndividuel = 75m, TarifHoraireCollectif = 40m,
            TarifHoraireComplementFormatif = 30m, FraisJury = 350m,
            PlafondHeuresIndividuel = 30m, PlafondHeuresCollectif = 20m,
            PlafondHeuresComplementFormatif = 70m, PlafondMontantTotal = 5850m,
            CoutHoraireIntervenant = 30m, HeuresArchitectureParDossier = 3m, CoutHoraireArchitecte = 35m,
        }]));

    /// <summary>Dossier sain : aucun déclenchement attendu.</summary>
    private static Parcours DossierConforme() => new()
    {
        Id = 1,
        Etape = EtapeParcours.Accompagnement,
        AapId = 1,
        AccompagnateurId = 2,
        DateDemande = Aujourdhui.AddDays(-60),
        DatePremierContact = Aujourdhui.AddDays(-58),
        DateRecueilBesoins = Aujourdhui.AddDays(-55),
        DateDernierMouvement = Aujourdhui.AddDays(-3),
        HeuresIndividuelPrescrites = 20m,
        Financements = [new Financement { DateSecurisation = Aujourdhui.AddDays(-40) }],
    };

    private static string[] Codes(Parcours p) =>
        Moteur().Evaluer(p, Aujourdhui).Select(a => a.Regle.Code).ToArray();

    [Fact]
    public void Un_dossier_sain_ne_declenche_aucune_alerte()
    {
        Assert.Empty(Codes(DossierConforme()));
    }

    [Fact]
    public void R01_se_declenche_au_dela_de_huit_jours_ouvres_sans_rdv()
    {
        var p = DossierConforme();
        p.DateRecueilBesoins = null;
        p.DatePremierContact = Aujourdhui.AddDays(-20); // ~14 jours ouvrés

        Assert.Contains("R01", Codes(p));
    }

    [Fact]
    public void R01_ne_se_declenche_pas_dans_le_delai()
    {
        var p = DossierConforme();
        p.DateRecueilBesoins = null;
        p.DateDemande = Aujourdhui.AddDays(-8);
        p.DatePremierContact = Aujourdhui.AddDays(-7); // 5 jours ouvrés

        Assert.DoesNotContain("R01", Codes(p));
    }

    [Fact]
    public void R02_se_declenche_sans_financement_securise()
    {
        var p = DossierConforme();
        p.Financements = [new Financement { DateSecurisation = null }];

        Assert.Contains("R02", Codes(p));
    }

    [Fact]
    public void R02_se_leve_des_que_le_financement_est_securise()
    {
        var p = DossierConforme();
        p.Financements = [new Financement { DateSecurisation = Aujourdhui }];

        Assert.DoesNotContain("R02", Codes(p));
    }

    [Theory]
    [InlineData(10, null)]   // dans les clous
    [InlineData(30, "R04")]  // ralenti
    [InlineData(60, "R03")]  // sans évolution
    public void L_inactivite_bascule_de_la_vigilance_au_critique(int joursSansMouvement, string? attendu)
    {
        var p = DossierConforme();
        p.DateDernierMouvement = Aujourdhui.AddDays(-joursSansMouvement);

        var codes = Codes(p);
        if (attendu is null)
        {
            Assert.DoesNotContain("R03", codes);
            Assert.DoesNotContain("R04", codes);
        }
        else
        {
            Assert.Contains(attendu, codes);
        }
    }

    [Fact]
    public void R05_signale_une_recevabilite_hors_delai_reglementaire()
    {
        var p = DossierConforme();
        p.Etape = EtapeParcours.Faisabilite;
        p.DateDepotFaisabilite = Aujourdhui.AddDays(-90); // plafond : 2 mois

        Assert.Contains("R05", Codes(p));
    }

    [Fact]
    public void R06_signale_un_jury_non_organise_apres_trois_mois()
    {
        var p = DossierConforme();
        p.Etape = EtapeParcours.PreparationJury;
        p.DateDepotDossierValidation = Aujourdhui.AddDays(-120);

        Assert.Contains("R06", Codes(p));
    }

    [Fact]
    public void R07_prepare_un_jury_proche_sans_alerter_sur_un_jury_lointain()
    {
        var proche = DossierConforme();
        proche.DateJury = Aujourdhui.AddDays(15);
        Assert.Contains("R07", Codes(proche));

        var lointain = DossierConforme();
        lointain.DateJury = Aujourdhui.AddDays(120);
        Assert.DoesNotContain("R07", Codes(lointain));
    }

    [Fact]
    public void R08_alerte_quand_l_enveloppe_est_presque_consommee()
    {
        var p = DossierConforme();
        p.HeuresIndividuelPrescrites = 20m;
        p.Seances =
        [
            new Seance { DureeHeures = 12m, Realisee = true, Nature = NatureHeure.Individuel },
            new Seance { DureeHeures = 6m, Realisee = true, Nature = NatureHeure.Individuel },
        ];

        Assert.Contains("R08", Codes(p)); // 18 h sur 20 h = 90 %
    }

    [Fact]
    public void Les_heures_asynchrones_ne_comptent_pas_dans_la_consommation()
    {
        var p = DossierConforme();
        p.HeuresIndividuelPrescrites = 20m;
        p.Seances =
        [
            new Seance { DureeHeures = 5m, Realisee = true, Nature = NatureHeure.Individuel },
            new Seance { DureeHeures = 15m, Realisee = true, Nature = NatureHeure.Asynchrone },
        ];

        Assert.DoesNotContain("R08", Codes(p));
    }

    [Fact]
    public void Une_seance_planifiee_ne_compte_pas_comme_realisee()
    {
        var p = DossierConforme();
        p.HeuresIndividuelPrescrites = 20m;
        p.Seances = [new Seance { DureeHeures = 19m, Realisee = false, Nature = NatureHeure.Individuel }];

        Assert.DoesNotContain("R08", Codes(p));
    }

    [Fact]
    public void R12_signale_une_prestation_realisee_non_facturee()
    {
        var p = DossierConforme();
        p.Etape = EtapeParcours.PostJury;
        p.Factures = [];

        Assert.Contains("R12", Codes(p));
    }

    [Fact]
    public void R12_ne_se_declenche_pas_si_la_facture_existe()
    {
        var p = DossierConforme();
        p.Etape = EtapeParcours.PostJury;
        p.Factures = [new Facture { Numero = "FA001", MontantHt = 2280m }];

        Assert.DoesNotContain("R12", Codes(p));
    }

    [Fact]
    public void Un_dossier_clos_ne_declenche_plus_les_alertes_de_suivi()
    {
        var p = DossierConforme();
        p.Etape = EtapeParcours.Cloture;
        p.DateDernierMouvement = Aujourdhui.AddDays(-400);
        p.AapId = null;

        var codes = Codes(p);
        Assert.DoesNotContain("R03", codes);
        Assert.DoesNotContain("R14", codes);
    }

    [Theory]
    [InlineData("2026-08-17", "2026-08-20", 3)]  // lundi -> jeudi
    [InlineData("2026-08-14", "2026-08-17", 1)]  // vendredi -> lundi : week-end exclu
    [InlineData("2026-08-20", "2026-08-20", 0)]
    public void Les_jours_ouvres_excluent_les_week_ends(string debut, string fin, int attendu)
    {
        Assert.Equal(attendu, MoteurAlertes.JoursOuvres(DateOnly.Parse(debut), DateOnly.Parse(fin)));
    }

    [Fact]
    public void Les_regles_par_defaut_ont_des_codes_uniques_et_une_action()
    {
        var regles = MoteurAlertes.ReglesParDefaut();
        Assert.Equal(15, regles.Count);
        Assert.Equal(regles.Count, regles.Select(r => r.Code).Distinct().Count());
        Assert.All(regles, r =>
        {
            Assert.False(string.IsNullOrWhiteSpace(r.ActionAttendue));
            Assert.False(string.IsNullOrWhiteSpace(r.Responsable));
            Assert.False(string.IsNullOrWhiteSpace(MoteurAlertes.DecrireSeuil(r)));
        });
    }

    [Fact]
    public void R15_signale_un_dossier_en_attente_du_candidat()
    {
        var p = DossierConforme();
        p.StatutSecondaire = StatutSecondaire.AttenteCandidat;

        Assert.Contains("R15", Codes(p));
    }

    [Fact]
    public void Une_regle_desactivee_ne_se_declenche_plus()
    {
        var regles = MoteurAlertes.ReglesParDefaut();
        regles.Single(r => r.Code == "R14").Active = false;

        var p = DossierConforme();
        p.AapId = null;

        Assert.DoesNotContain("R14",
            Moteur(regles).Evaluer(p, Aujourdhui).Select(a => a.Regle.Code));
    }

    [Fact]
    public void Une_regle_creee_par_le_service_est_evaluee_sans_modifier_le_code()
    {
        var regles = MoteurAlertes.ReglesParDefaut().Append(new RegleAlerte
        {
            Code = "R20",
            Libelle = "Dossier de validation non déposé",
            Condition = TypeCondition.DelaiDepuisJalon,
            JalonReference = nameof(Parcours.DateDebutAccompagnement),
            JalonAttendu = nameof(Parcours.DateDepotDossierValidation),
            Seuil = 120,
            EtapeMin = EtapeParcours.Accompagnement,
            ActionAttendue = "Fixer une date de dépôt",
        }).ToList();

        var p = DossierConforme();
        p.DateDebutAccompagnement = Aujourdhui.AddDays(-150);
        Assert.Contains("R20", Moteur(regles).Evaluer(p, Aujourdhui).Select(a => a.Regle.Code));

        p.DateDepotDossierValidation = Aujourdhui.AddDays(-10);
        Assert.DoesNotContain("R20", Moteur(regles).Evaluer(p, Aujourdhui).Select(a => a.Regle.Code));
    }

    [Fact]
    public void Les_bornes_d_etape_limitent_la_regle()
    {
        var p = DossierConforme();
        p.Etape = EtapeParcours.Financement;
        p.Financements = [];

        // Le financement se sécurise pendant l'étape Financement : R02 ne s'applique qu'après.
        Assert.DoesNotContain("R02", Codes(p));

        p.Etape = EtapeParcours.Faisabilite;
        Assert.Contains("R02", Codes(p));
    }

    [Fact]
    public void Le_financement_precede_la_faisabilite_et_la_recevabilite_precede_la_validation()
    {
        Assert.True(EtapeParcours.Financement < EtapeParcours.Faisabilite);
        Assert.True(EtapeParcours.Recevabilite < EtapeParcours.ParcoursValide);
        Assert.Equal(4, (int)EtapeParcours.Financement);
        Assert.Equal(7, (int)EtapeParcours.ParcoursValide);
    }
}
