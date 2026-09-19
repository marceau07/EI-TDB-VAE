namespace EiVae.Domain.Entities;

/// <summary>
/// Référentiel des certifications, indépendant des dossiers candidats.
///
/// Les champs marqués « France Compétences » sont alimentés automatiquement par
/// l'import quotidien de l'export RNCP et ne doivent pas être saisis à la main :
/// toute modification serait écrasée à la synchronisation suivante. Les champs
/// marqués « interne » appartiennent à EI Groupe et ne sont jamais touchés par
/// l'import.
/// </summary>
public sealed class Certification
{
    public int Id { get; set; }

    // ---------- France Compétences (synchronisé) ----------

    /// <summary>Code au format « RNCP37275 ».</summary>
    public required string CodeRncp { get; set; }

    public string? IdFiche { get; set; }
    public required string Intitule { get; set; }

    /// <summary>« Publiée », « Archivée »…</summary>
    public string? EtatFiche { get; set; }

    /// <summary>Fiche publiée et enregistrement non expiré.</summary>
    public bool ActifFranceCompetences { get; set; }

    public int? Niveau { get; set; }
    public string? LibelleNiveau { get; set; }
    public string? TypeEnregistrement { get; set; }

    public DateOnly? DateDecision { get; set; }
    public DateOnly? DateFinEnregistrement { get; set; }
    public DateOnly? DateLimiteDelivrance { get; set; }
    public DateOnly? DateDerniereModificationFiche { get; set; }

    /// <summary>La certification est-elle accessible par la voie de la VAE ?</summary>
    public bool VoieVaeOuverte { get; set; }

    /// <summary>Composition du jury VAE telle que déclarée par le certificateur.</summary>
    public string? CompositionJuryVae { get; set; }

    public bool VoieFormationInitiale { get; set; }
    public bool VoieFormationContinue { get; set; }
    public bool VoieApprentissage { get; set; }
    public bool VoieContratProfessionnalisation { get; set; }
    public bool VoieCandidatLibre { get; set; }

    public string? ActivitesVisees { get; set; }
    public string? CapacitesAttestees { get; set; }
    public string? SecteursActivite { get; set; }
    public string? TypeEmploiAccessibles { get; set; }
    public string? ObjectifsContexte { get; set; }
    public string? Prerequis { get; set; }
    public string? ReglementationActivites { get; set; }

    /// <summary>Codes NSF, Formacode et ROME, conservés tels quels (JSONB).</summary>
    public string? CodesNsfJson { get; set; }
    public string? FormacodesJson { get; set; }
    public string? CodesRomeJson { get; set; }

    /// <summary>Statistiques de promotion, dont le nombre de certifiés par VAE (JSONB).</summary>
    public string? StatistiquesJson { get; set; }

    public DateTimeOffset? DerniereSynchronisation { get; set; }

    // ---------- interne EI Groupe (jamais écrasé) ----------

    /// <summary>Abrégé utilisé au quotidien par le service : « TP FPA », « DE EJE »…</summary>
    public string? Abrege { get; set; }

    /// <summary>Domaine métier EI Groupe, plus large que la nomenclature NSF.</summary>
    public string? DomaineEi { get; set; }

    public StatutCertificationInterne StatutInterne { get; set; } = StatutCertificationInterne.NonCouverte;

    /// <summary>Durée habituelle observée d'un parcours, en jours.</summary>
    public int? DureeHabituelleJours { get; set; }

    /// <summary>Particularités d'accompagnement : prérequis métier, stage, habilitation…</summary>
    public string? Particularites { get; set; }

    /// <summary>Contact opérationnel chez le certificateur (service recevabilité, jury).</summary>
    public string? ContactCertificateurNom { get; set; }
    public string? ContactCertificateurEmail { get; set; }
    public string? ContactCertificateurTelephone { get; set; }

    public DateTimeOffset CreeLe { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ModifieLe { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<BlocCompetences> Blocs { get; set; } = [];
    public ICollection<CertificationCertificateur> Certificateurs { get; set; } = [];
    public ICollection<HabilitationIntervenant> Habilitations { get; set; } = [];
    public ICollection<CertificationModule> Modules { get; set; } = [];
    public ICollection<Parcours> Parcours { get; set; } = [];

    /// <summary>Lien direct vers la fiche publique, construit depuis le code RNCP.</summary>
    public string LienFranceCompetences =>
        $"https://www.francecompetences.fr/recherche/rncp/{CodeRncp.Replace("RNCP", string.Empty)}/";
}

/// <summary>Bloc de compétences tel que publié par France Compétences.</summary>
public sealed class BlocCompetences
{
    public int Id { get; set; }
    public int CertificationId { get; set; }
    public Certification? Certification { get; set; }

    /// <summary>Code au format « RNCP37275BC01 ».</summary>
    public required string Code { get; set; }

    public required string Libelle { get; set; }
    public string? Competences { get; set; }
    public string? ModalitesEvaluation { get; set; }
    public int Ordre { get; set; }
}

/// <summary>Organisme certificateur, identifié par son SIRET.</summary>
public sealed class Certificateur
{
    public int Id { get; set; }
    public string? Siret { get; set; }
    public required string Nom { get; set; }
    public string? Etat { get; set; }

    /// <summary>Contact opérationnel générique, complété par le service.</summary>
    public string? ContactEmail { get; set; }
    public string? ContactTelephone { get; set; }
    public string? Notes { get; set; }

    public ICollection<CertificationCertificateur> Certifications { get; set; } = [];
}

public sealed class CertificationCertificateur
{
    public int CertificationId { get; set; }
    public Certification? Certification { get; set; }
    public int CertificateurId { get; set; }
    public Certificateur? Certificateur { get; set; }
    public bool EstPrincipal { get; set; }
}

/// <summary>
/// Habilitation d'un intervenant sur une certification : c'est cette table qui
/// répond à « qui peut accompagner cette certification ? ».
/// </summary>
public sealed class HabilitationIntervenant
{
    public int Id { get; set; }
    public int IntervenantId { get; set; }
    public Intervenant? Intervenant { get; set; }
    public int CertificationId { get; set; }
    public Certification? Certification { get; set; }

    /// <summary>Habilité, en cours de montée en compétences, ou pressenti.</summary>
    public required string Niveau { get; set; }

    public DateOnly? DateHabilitation { get; set; }
    public string? Commentaire { get; set; }
}

/// <summary>Module EI Académie mobilisable en complément formatif ou en socle.</summary>
public sealed class ModuleAcademie
{
    public int Id { get; set; }
    public required string Code { get; set; }
    public required string Titre { get; set; }

    public NatureModule Nature { get; set; } = NatureModule.ELearning;

    /// <summary>Sous-catégorie libre : Transversal, Métier, Méthodologie…</summary>
    public required string Type { get; set; }

    public decimal? DureeHeures { get; set; }
    public string? Url { get; set; }
    public string? Description { get; set; }
    public bool Actif { get; set; } = true;

    public ICollection<CertificationModule> Certifications { get; set; } = [];
    public ICollection<ParcoursModule> Parcours { get; set; } = [];
}

/// <summary>Module e-learning ou complément formatif inclus dans un parcours.</summary>
public sealed class ParcoursModule
{
    public int ParcoursId { get; set; }
    public Parcours? Parcours { get; set; }
    public int ModuleAcademieId { get; set; }
    public ModuleAcademie? ModuleAcademie { get; set; }

    public StatutModule Statut { get; set; } = StatutModule.Prescrit;
    public DateOnly DateAttribution { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    public DateOnly? DateFin { get; set; }
}

public sealed class CertificationModule
{
    public int CertificationId { get; set; }
    public Certification? Certification { get; set; }
    public int ModuleAcademieId { get; set; }
    public ModuleAcademie? ModuleAcademie { get; set; }

    /// <summary>Le module fait partie du socle prescrit d'office pour cette certification.</summary>
    public bool Obligatoire { get; set; }
}
