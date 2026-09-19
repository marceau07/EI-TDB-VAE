using EiVae.Domain.Entities;

namespace EiVae.Domain.Services;

/// <summary>
/// Choix de la grille tarifaire et calcul économique d'un parcours.
///
/// Règle de gestion retenue : un parcours est facturé à la grille en vigueur au
/// jour de son démarrage. La revalorisation du 15 juin 2026 ne s'applique donc
/// qu'aux parcours démarrés à partir de cette date ; les parcours antérieurs
/// restent à la grille 2025 jusqu'à leur clôture, y compris pour les heures
/// réalisées après cette date. C'est la règle qui protège la cohérence entre le
/// devis signé, les prestations délivrées et la facture — exigence de la Caisse
/// des Dépôts sur les parcours financés par le CPF.
/// </summary>
public sealed class TarificationService
{
    private readonly IReadOnlyList<GrilleTarifaire> _grilles;

    public TarificationService(IEnumerable<GrilleTarifaire> grilles)
    {
        _grilles = grilles.OrderBy(g => g.DateEffet).ToList();
        if (_grilles.Count == 0)
        {
            throw new ArgumentException(
                "Aucune grille tarifaire n'est chargée : le calcul économique est impossible.",
                nameof(grilles));
        }
    }

    /// <summary>
    /// Grille applicable à une date de démarrage : la plus récente dont la date
    /// d'effet est atteinte. Une date antérieure à toute grille connue retombe
    /// sur la plus ancienne, pour que les dossiers historiques restent calculables.
    /// </summary>
    public GrilleTarifaire GrillePour(DateOnly dateDebutParcours)
    {
        GrilleTarifaire? retenue = null;
        foreach (var g in _grilles)
        {
            if (g.DateEffet <= dateDebutParcours)
            {
                retenue = g;
            }
        }

        return retenue ?? _grilles[0];
    }

    /// <summary>
    /// Grille applicable à un parcours. Si une grille a été figée sur le dossier
    /// au démarrage, elle prime : c'est elle qui a servi au devis signé.
    /// </summary>
    public GrilleTarifaire GrillePour(Parcours parcours)
    {
        if (parcours.GrilleTarifaire is not null)
        {
            return parcours.GrilleTarifaire;
        }

        var debut = parcours.DateDebutParcours
                    ?? parcours.CalculerDateDebut()
                    ?? DateOnly.FromDateTime(DateTime.UtcNow);

        return GrillePour(debut);
    }

    /// <summary>
    /// Calcule le montant d'un parcours à partir des heures réellement prescrites.
    /// Les plafonds ne sont jamais appliqués d'office : ils produisent un
    /// signalement, à charge du service d'arbitrer.
    /// </summary>
    public CalculParcours Calculer(Parcours parcours, int participantsCollectif = 1)
    {
        var grille = GrillePour(parcours);
        return Calculer(
            grille,
            parcours.ForfaitArchitectureApplique,
            parcours.HeuresIndividuelPrescrites,
            parcours.HeuresCollectifPrescrites,
            parcours.HeuresComplementFormatifPrescrites,
            parcours.FraisJuryInclus,
            participantsCollectif);
    }

    public CalculParcours Calculer(
        GrilleTarifaire grille,
        bool forfaitArchitecture,
        decimal heuresIndividuel,
        decimal heuresCollectif,
        decimal heuresComplementFormatif,
        bool fraisJuryInclus,
        int participantsCollectif = 1)
    {
        if (participantsCollectif < 1)
        {
            participantsCollectif = 1;
        }

        var forfait = forfaitArchitecture ? grille.ForfaitArchitecture : 0m;
        var jury = fraisJuryInclus ? grille.FraisJury : 0m;

        var montantIndividuel = heuresIndividuel * grille.TarifHoraireIndividuel;
        var montantCollectif = heuresCollectif * grille.TarifHoraireCollectif;
        var montantComplement = heuresComplementFormatif * grille.TarifHoraireComplementFormatif;

        var totalParCandidat = forfait + jury + montantIndividuel + montantCollectif + montantComplement;
        var total = totalParCandidat * participantsCollectif;

        // Coût pédagogique : seules les heures synchrones mobilisent un intervenant.
        // Une heure collective est animée une fois pour tout le groupe, alors que
        // les heures individuelles se répètent pour chaque candidat. Les compléments
        // formatifs s'appuient sur des contenus EI Académie déjà produits : leur
        // coût marginal est nul.
        var heuresMobilisees =
            heuresIndividuel * participantsCollectif
            + heuresCollectif
            + (forfaitArchitecture ? grille.HeuresArchitectureParDossier * participantsCollectif : 0m);

        var coutAccompagnement =
            (heuresIndividuel * participantsCollectif + heuresCollectif) * grille.CoutHoraireIntervenant;

        var coutArchitecture = forfaitArchitecture
            ? grille.HeuresArchitectureParDossier * grille.CoutHoraireArchitecte * participantsCollectif
            : 0m;

        var cout = coutAccompagnement + coutArchitecture;

        var depassements = new List<string>();
        if (heuresIndividuel > grille.PlafondHeuresIndividuel)
        {
            depassements.Add(
                $"Accompagnement individuel : {heuresIndividuel:0.#} h prescrites pour un plafond de {grille.PlafondHeuresIndividuel:0.#} h.");
        }

        if (heuresCollectif > grille.PlafondHeuresCollectif)
        {
            depassements.Add(
                $"Accompagnement collectif : {heuresCollectif:0.#} h prescrites pour un plafond de {grille.PlafondHeuresCollectif:0.#} h.");
        }

        if (heuresComplementFormatif > grille.PlafondHeuresComplementFormatif)
        {
            depassements.Add(
                $"Compléments formatifs : {heuresComplementFormatif:0.#} h prescrites pour un plafond de {grille.PlafondHeuresComplementFormatif:0.#} h.");
        }

        if (totalParCandidat > grille.PlafondMontantTotal)
        {
            depassements.Add(
                $"Montant par candidat : {totalParCandidat:0} € pour un plafond mobilisable de {grille.PlafondMontantTotal:0} €.");
        }

        _ = heuresMobilisees;

        return new CalculParcours(
            grille.Code,
            Arrondi(forfait * participantsCollectif),
            Arrondi(montantIndividuel * participantsCollectif),
            Arrondi(montantCollectif * participantsCollectif),
            Arrondi(montantComplement * participantsCollectif),
            Arrondi(jury * participantsCollectif),
            Arrondi(total),
            Arrondi(cout),
            Arrondi(total - cout),
            depassements);
    }

    /// <summary>
    /// Chiffre d'affaires produit par heure d'intervenant réellement mobilisée.
    /// C'est l'indicateur qui compare honnêtement un parcours individuel et une
    /// cohorte : le chiffre d'affaires brut, lui, ne dit rien du coût de production.
    /// </summary>
    public decimal CaParHeureMobilisee(
        GrilleTarifaire grille,
        bool forfaitArchitecture,
        decimal heuresIndividuel,
        decimal heuresCollectif,
        decimal heuresComplementFormatif,
        bool fraisJuryInclus,
        int participants)
    {
        var calcul = Calculer(
            grille, forfaitArchitecture, heuresIndividuel, heuresCollectif,
            heuresComplementFormatif, fraisJuryInclus, participants);

        var heures =
            heuresIndividuel * participants
            + heuresCollectif
            + (forfaitArchitecture ? grille.HeuresArchitectureParDossier * participants : 0m);

        return heures == 0 ? 0 : Arrondi(calcul.Total / heures);
    }

    private static decimal Arrondi(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);
}
