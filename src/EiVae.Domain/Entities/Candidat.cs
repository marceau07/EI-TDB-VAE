namespace EiVae.Domain.Entities;

/// <summary>
/// La personne. Distincte du parcours : un candidat peut engager plusieurs
/// parcours (réorientation, seconde certification, blocs complémentaires).
/// </summary>
public sealed class Candidat
{
    public int Id { get; set; }

    public required string Nom { get; set; }
    public required string Prenom { get; set; }

    public string? Email { get; set; }
    public string? Telephone { get; set; }

    public string? Ville { get; set; }
    public string? CodePostal { get; set; }

    /// <summary>Département sur deux ou trois caractères : « 34 », « 974 ».</summary>
    public string? Departement { get; set; }
    public string? Region { get; set; }

    public DateOnly? DateNaissance { get; set; }

    /// <summary>Identifiant du candidat sur France VAE, quand il est connu.</summary>
    public string? IdentifiantFranceVae { get; set; }

    public string? Notes { get; set; }

    /// <summary>Consentement au traitement des données, requis pour la conservation.</summary>
    public bool ConsentementRgpd { get; set; }
    public DateOnly? DateConsentementRgpd { get; set; }

    public DateTimeOffset CreeLe { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ModifieLe { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<Parcours> Parcours { get; set; } = [];

    public string NomComplet => $"{Prenom} {Nom}";
}

/// <summary>
/// Le dossier VAE : c'est l'objet que le service pilote. Il porte l'étape, les
/// jalons, les acteurs, les heures, le financement et la grille tarifaire.
/// </summary>
public sealed class Parcours
{
    public int Id { get; set; }

    public int CandidatId { get; set; }
    public Candidat? Candidat { get; set; }

    public int? CertificationId { get; set; }
    public Certification? Certification { get; set; }

    /// <summary>Codes des blocs visés, quand le parcours ne vise pas la certification entière.</summary>
    public string? BlocsVisesJson { get; set; }

    public EtapeParcours Etape { get; set; } = EtapeParcours.DemandeRecue;
    public StatutSecondaire StatutSecondaire { get; set; } = StatutSecondaire.Aucun;
    public MotifSortie MotifSortie { get; set; } = MotifSortie.Aucun;
    public string? CommentaireSortie { get; set; }

    public OrigineCandidature Origine { get; set; } = OrigineCandidature.FranceVae;

    // ---------- acteurs ----------
    public int? AapId { get; set; }
    public Intervenant? Aap { get; set; }

    public int? AccompagnateurId { get; set; }
    public Intervenant? Accompagnateur { get; set; }

    /// <summary>Gestionnaire administratif du dossier.</summary>
    public int? GestionnaireId { get; set; }
    public Intervenant? Gestionnaire { get; set; }

    // ---------- jalons ----------
    public DateOnly? DateDemande { get; set; }
    public DateOnly? DatePremierContact { get; set; }
    public DateOnly? DateRecueilBesoins { get; set; }
    public DateOnly? DateRdvFaisabilite { get; set; }
    public DateOnly? DateDepotFaisabilite { get; set; }
    public DateOnly? DateParcoursValide { get; set; }
    public DateOnly? DateRecevabilite { get; set; }
    public DateOnly? DateDebutAccompagnement { get; set; }
    public DateOnly? DateDepotDossierValidation { get; set; }
    public DateOnly? DateJury { get; set; }
    public DateOnly? DateEntretienPostJury { get; set; }

    /// <summary>
    /// Date de démarrage effectif du parcours. C'est <em>elle</em> qui détermine la
    /// grille tarifaire applicable. Renseignée automatiquement à la première des
    /// valeurs disponibles — parcours validé, dépôt de faisabilité, demande —
    /// et modifiable manuellement si le service en décide autrement.
    /// </summary>
    public DateOnly? DateDebutParcours { get; set; }

    /// <summary>Grille figée au démarrage : une revalorisation ne s'applique pas rétroactivement.</summary>
    public int? GrilleTarifaireId { get; set; }
    public GrilleTarifaire? GrilleTarifaire { get; set; }

    // ---------- heures prescrites ----------
    public decimal HeuresIndividuelPrescrites { get; set; }
    public decimal HeuresCollectifPrescrites { get; set; }
    public decimal HeuresComplementFormatifPrescrites { get; set; }

    /// <summary>Le forfait architecture s'applique à ce parcours.</summary>
    public bool ForfaitArchitectureApplique { get; set; } = true;

    /// <summary>Les frais de jury sont inclus au devis.</summary>
    public bool FraisJuryInclus { get; set; }

    // ---------- résultat ----------
    public ResultatJury ResultatJury { get; set; } = ResultatJury.NonRenseigne;
    public string? BlocsValidesJson { get; set; }
    public string? CommentaireJury { get; set; }

    // ---------- systèmes tiers ----------
    /// <summary>Identifiant de la candidature sur France VAE (UUID).</summary>
    public string? CandidatureFranceVaeId { get; set; }

    /// <summary>Code action de formation dans Solei.</summary>
    public string? CodeAcfSolei { get; set; }

    /// <summary>Espace candidat EI Académie créé.</summary>
    public bool EspaceAcademieCree { get; set; }
    public string? UrlEspaceAcademie { get; set; }
    public DateOnly? DateDerniereActiviteAcademie { get; set; }

    /// <summary>
    /// Chemin relatif du dossier candidat dans la bibliothèque SharePoint.
    /// L'URL absolue est construite par le générateur de liens à partir de la
    /// configuration du site : le chemin stocké reste valable si le site déménage.
    /// </summary>
    public string? SharePointDossier { get; set; }

    // ---------- VAE collective ----------
    public int? CohorteId { get; set; }
    public Cohorte? Cohorte { get; set; }

    /// <summary>Dernier mouvement constaté, tous canaux confondus. Base des alertes d'inactivité.</summary>
    public DateOnly? DateDernierMouvement { get; set; }

    public string? Historique { get; set; }

    public DateTimeOffset CreeLe { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ModifieLe { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<Seance> Seances { get; set; } = [];
    public ICollection<Financement> Financements { get; set; } = [];
    public ICollection<Facture> Factures { get; set; } = [];
    public ICollection<PieceDossier> Pieces { get; set; } = [];
    public ICollection<HistoriqueStatut> HistoriqueStatuts { get; set; } = [];
    public ICollection<AlerteInstance> Alertes { get; set; } = [];
    public ICollection<ParcoursModule> Modules { get; set; } = [];

    public bool EstActif => Etape is not (EtapeParcours.Cloture or EtapeParcours.Sortie);

    public decimal HeuresPrescritesTotales =>
        HeuresIndividuelPrescrites + HeuresCollectifPrescrites + HeuresComplementFormatifPrescrites;

    /// <summary>
    /// Recalcule la date de démarrage à partir des jalons disponibles. Ne l'écrase
    /// pas si elle a été fixée manuellement.
    /// </summary>
    public DateOnly? CalculerDateDebut() =>
        DateParcoursValide ?? DateDepotFaisabilite ?? DateRecueilBesoins ?? DateDemande;
}

/// <summary>Trace d'un changement d'étape : sert au calcul des délais réels.</summary>
public sealed class HistoriqueStatut
{
    public long Id { get; set; }
    public int ParcoursId { get; set; }
    public Parcours? Parcours { get; set; }

    public EtapeParcours? EtapePrecedente { get; set; }
    public EtapeParcours EtapeNouvelle { get; set; }
    public StatutSecondaire StatutSecondaire { get; set; }

    public DateTimeOffset SurvenuLe { get; set; } = DateTimeOffset.UtcNow;
    public string? Auteur { get; set; }
    public string? Commentaire { get; set; }
}

/// <summary>
/// Une séance d'accompagnement réalisée. Remplace la reconstitution des heures
/// depuis des colonnes de suivi en texte libre : c'est la pièce qui sécurise la
/// cohérence entre le devis et les prestations réellement délivrées.
/// </summary>
public sealed class Seance
{
    public int Id { get; set; }
    public int ParcoursId { get; set; }
    public Parcours? Parcours { get; set; }

    public int? IntervenantId { get; set; }
    public Intervenant? Intervenant { get; set; }

    public DateOnly Date { get; set; }
    public NatureHeure Nature { get; set; }
    public decimal DureeHeures { get; set; }

    /// <summary>Présentiel, distanciel, asynchrone.</summary>
    public string? Modalite { get; set; }

    public bool Emargee { get; set; }
    public string? UrlEmargement { get; set; }
    public string? Objet { get; set; }
    public string? Commentaire { get; set; }

    /// <summary>Séance planifiée non encore réalisée : comptée en prévisionnel, pas en réalisé.</summary>
    public bool Realisee { get; set; } = true;

    public DateTimeOffset CreeLe { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>Dispositif de financement mobilisé sur un parcours.</summary>
public sealed class Financement
{
    public int Id { get; set; }
    public int ParcoursId { get; set; }
    public Parcours? Parcours { get; set; }

    public DispositifFinancement Dispositif { get; set; }
    public string? Financeur { get; set; }

    /// <summary>Numéro de prise en charge, de dossier CPF, ou référence France VAE.</summary>
    public string? NumeroPriseEnCharge { get; set; }

    public decimal? MontantAccorde { get; set; }
    public decimal? ResteACharge { get; set; }

    public DateOnly? DateDemande { get; set; }

    /// <summary>Date à laquelle le financement est réputé sécurisé. Prérequis à la validation du parcours.</summary>
    public DateOnly? DateSecurisation { get; set; }

    public string? Commentaire { get; set; }

    public bool EstSecurise => DateSecurisation is not null;
}

/// <summary>Facture émise sur un parcours.</summary>
public sealed class Facture
{
    public int Id { get; set; }
    public int ParcoursId { get; set; }
    public Parcours? Parcours { get; set; }

    public required string Numero { get; set; }
    public DateOnly DateEmission { get; set; }
    public string? Financeur { get; set; }
    public decimal MontantHt { get; set; }

    public DateOnly? DateReglement { get; set; }
    public string? CodeAcf { get; set; }
    public string? Commentaire { get; set; }

    public bool EstReglee => DateReglement is not null;
}

/// <summary>
/// Pièce attendue au dossier. La liste des pièces obligatoires par étape est le
/// support des contrôles Qualiopi et France VAE.
/// </summary>
public sealed class PieceDossier
{
    public int Id { get; set; }
    public int ParcoursId { get; set; }
    public Parcours? Parcours { get; set; }

    public required string Type { get; set; }
    public required string Libelle { get; set; }

    /// <summary>Étape à partir de laquelle la pièce devient exigible.</summary>
    public EtapeParcours ExigibleAPartirDe { get; set; }

    public bool Obligatoire { get; set; } = true;
    public bool Presente { get; set; }
    public DateOnly? DateDepot { get; set; }

    /// <summary>Chemin relatif du fichier dans le dossier SharePoint du candidat.</summary>
    public string? CheminSharePoint { get; set; }

    public string? Commentaire { get; set; }
}
