namespace EiVae.Domain.Entities;

/// <summary>
/// Toute personne mobilisée sur un parcours : AAP, accompagnateur, expert métier
/// ou fonction interne. Une seule table, pour que la charge et les habilitations
/// se pilotent au même endroit.
/// </summary>
public sealed class Intervenant
{
    public int Id { get; set; }

    public required string Nom { get; set; }
    public string? Prenom { get; set; }
    public TypeIntervenant Type { get; set; }
    public StatutIntervenant Statut { get; set; } = StatutIntervenant.Actif;

    public string? Email { get; set; }
    public string? Telephone { get; set; }

    /// <summary>Territoire d'intervention en présentiel.</summary>
    public string? Territoire { get; set; }
    public string? Region { get; set; }

    public bool InterventionDistanciel { get; set; } = true;
    public bool InterventionPresentiel { get; set; } = true;

    /// <summary>Spécialités déclarées, en texte libre : sert d'appoint aux habilitations.</summary>
    public string? Specialites { get; set; }

    /// <summary>Tarif de rémunération, en euros par heure.</summary>
    public decimal? TarifHoraire { get; set; }

    /// <summary>Nombre de candidats simultanés soutenables. Null : valeur par défaut du service.</summary>
    public int? CapaciteCandidats { get; set; }

    /// <summary>Heures mobilisables par trimestre, pour le calcul de disponibilité.</summary>
    public decimal? CapaciteHeuresTrimestre { get; set; }

    public DateOnly? DateEntreeReseau { get; set; }
    public DateOnly? DateSortieReseau { get; set; }

    /// <summary>Numéro SIRET pour les intervenants indépendants.</summary>
    public string? Siret { get; set; }

    public string? Notes { get; set; }

    /// <summary>Dossier RH de l'intervenant sur SharePoint.</summary>
    public string? SharePointDossier { get; set; }

    public DateTimeOffset CreeLe { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ModifieLe { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<HabilitationIntervenant> Habilitations { get; set; } = [];
    public ICollection<Seance> Seances { get; set; } = [];

    public string NomComplet => string.IsNullOrWhiteSpace(Prenom) ? Nom : $"{Prenom} {Nom}";

    public bool EstMobilisable =>
        Statut is StatutIntervenant.Actif or StatutIntervenant.EnIntegration;
}
