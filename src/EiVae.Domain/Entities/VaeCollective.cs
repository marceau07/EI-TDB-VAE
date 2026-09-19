namespace EiVae.Domain.Entities;

/// <summary>
/// Projet de VAE collective porté par une entreprise. Entité distincte du
/// candidat : sans elle, une cohorte serait gérée comme une addition de dossiers
/// isolés, ce qui annule l'essentiel du gain économique du dispositif.
/// </summary>
public sealed class ProjetCollectif
{
    public int Id { get; set; }

    public required string Nom { get; set; }

    // ---------- entreprise ----------
    public required string RaisonSociale { get; set; }
    public string? Siret { get; set; }
    public string? SecteurActivite { get; set; }
    public int? Effectif { get; set; }
    public string? ConventionCollective { get; set; }
    public string? Opco { get; set; }

    // ---------- référent entreprise ----------
    public string? ReferentNom { get; set; }
    public string? ReferentFonction { get; set; }
    public string? ReferentEmail { get; set; }
    public string? ReferentTelephone { get; set; }

    // ---------- pilotage ----------
    public int? AapReferentId { get; set; }
    public Intervenant? AapReferent { get; set; }

    /// <summary>Diagnostic, Négociation, Contractualisé, En cours, Clôturé, Abandonné.</summary>
    public required string Statut { get; set; }

    public DateOnly? DateDiagnostic { get; set; }
    public DateOnly? DateContractualisation { get; set; }
    public DateOnly? DateOuverture { get; set; }
    public DateOnly? DateCloturePrevue { get; set; }
    public DateOnly? DateCloture { get; set; }

    // ---------- financement ----------
    public DispositifFinancement Dispositif { get; set; } = DispositifFinancement.Entreprise;
    public decimal? MontantContractualise { get; set; }
    public string? ReferenceContrat { get; set; }

    public string? SharePointDossier { get; set; }
    public string? Commentaire { get; set; }

    public DateTimeOffset CreeLe { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ModifieLe { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<Cohorte> Cohortes { get; set; } = [];
}

/// <summary>
/// Groupe de candidats d'un même projet visant une même certification, avec un
/// calendrier collectif commun.
/// </summary>
public sealed class Cohorte
{
    public int Id { get; set; }

    public int ProjetCollectifId { get; set; }
    public ProjetCollectif? ProjetCollectif { get; set; }

    public required string Nom { get; set; }

    public int? CertificationId { get; set; }
    public Certification? Certification { get; set; }

    public DateOnly? DateOuverture { get; set; }
    public DateOnly? DateCloturePrevue { get; set; }

    /// <summary>Effectif visé, à comparer aux parcours effectivement rattachés.</summary>
    public int? EffectifCible { get; set; }

    /// <summary>Rythme des ateliers collectifs : hebdomadaire, mensuel…</summary>
    public string? Rythme { get; set; }

    public int? AnimateurId { get; set; }
    public Intervenant? Animateur { get; set; }

    public string? Commentaire { get; set; }

    public ICollection<Parcours> Parcours { get; set; } = [];
    public ICollection<AtelierCollectif> Ateliers { get; set; } = [];

    public DateTimeOffset CreeLe { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Atelier collectif planifié pour une cohorte. La présence est suivie par
/// candidat : le suivi collectif ne doit pas masquer un décrochage individuel.
/// </summary>
public sealed class AtelierCollectif
{
    public int Id { get; set; }
    public int CohorteId { get; set; }
    public Cohorte? Cohorte { get; set; }

    public required string Theme { get; set; }
    public DateOnly Date { get; set; }
    public decimal DureeHeures { get; set; }
    public string? Modalite { get; set; }

    public int? IntervenantId { get; set; }
    public Intervenant? Intervenant { get; set; }

    public bool Realise { get; set; }
    public string? UrlEmargement { get; set; }
}
