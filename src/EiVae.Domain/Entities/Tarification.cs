namespace EiVae.Domain.Entities;

/// <summary>
/// Une grille tarifaire datée. Le service en applique plusieurs simultanément :
/// un parcours reste facturé à la grille en vigueur au moment de son démarrage,
/// même si une nouvelle grille est publiée en cours de parcours.
///
/// Grilles chargées à l'initialisation :
///   « 2025 » — en vigueur jusqu'au 14/06/2026 : 300 € / 70 € / 35 € / 25 €
///   « 2026 » — en vigueur à partir du 15/06/2026 : 350 € / 75 € / 40 € / 30 €
/// </summary>
public sealed class GrilleTarifaire
{
    public int Id { get; set; }

    /// <summary>Identifiant court et stable, utilisé dans les exports et les factures.</summary>
    public required string Code { get; set; }

    public required string Libelle { get; set; }

    /// <summary>Premier jour d'application, inclus.</summary>
    public DateOnly DateEffet { get; set; }

    /// <summary>Dernier jour d'application, inclus. Null pour la grille courante.</summary>
    public DateOnly? DateFin { get; set; }

    /// <summary>Forfait architecture / faisabilité / suivi administratif / entretien post-jury.</summary>
    public decimal ForfaitArchitecture { get; set; }

    public decimal TarifHoraireIndividuel { get; set; }

    /// <summary>Tarif par heure <em>et par participant</em>.</summary>
    public decimal TarifHoraireCollectif { get; set; }

    public decimal TarifHoraireComplementFormatif { get; set; }

    public decimal FraisJury { get; set; }

    public decimal PlafondHeuresIndividuel { get; set; }
    public decimal PlafondHeuresCollectif { get; set; }
    public decimal PlafondHeuresComplementFormatif { get; set; }

    /// <summary>Plafond mobilisable pour une certification visée en totalité.</summary>
    public decimal PlafondMontantTotal { get; set; }

    /// <summary>Rémunération moyenne d'un accompagnateur externe : base du calcul de marge.</summary>
    public decimal CoutHoraireIntervenant { get; set; }

    /// <summary>Heures d'architecture mobilisées par dossier, pour le coût interne.</summary>
    public decimal HeuresArchitectureParDossier { get; set; }

    /// <summary>Coût horaire chargé d'un AAP interne.</summary>
    public decimal CoutHoraireArchitecte { get; set; }

    public bool EstApplicableLe(DateOnly date) =>
        date >= DateEffet && (DateFin is null || date <= DateFin.Value);
}

/// <summary>
/// Résultat du calcul économique d'un parcours. Les montants sont toujours
/// déduits des heures réellement prescrites — jamais des plafonds.
/// </summary>
public sealed record CalculParcours(
    string CodeGrille,
    decimal Forfait,
    decimal MontantIndividuel,
    decimal MontantCollectif,
    decimal MontantComplementFormatif,
    decimal FraisJury,
    decimal Total,
    decimal CoutPedagogique,
    decimal Marge,
    IReadOnlyList<string> Depassements)
{
    public decimal TauxMarge => Total == 0 ? 0 : Math.Round(Marge / Total, 4);
    public bool PlafondRespecte => Depassements.Count == 0;
}
