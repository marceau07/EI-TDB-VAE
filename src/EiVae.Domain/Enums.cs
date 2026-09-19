namespace EiVae.Domain;

/// <summary>
/// Les douze étapes du parcours VAE. L'ordre de l'énumération est l'ordre du
/// pipeline : il sert au calcul des taux de passage et aux comparaisons
/// « étape au moins égale à ». Les valeurs sont stockées en base : changer
/// l'ordre impose une migration qui renumérote les dossiers existants.
/// </summary>
public enum EtapeParcours
{
    DemandeRecue = 1,
    Qualification = 2,
    RecueilDesBesoins = 3,
    Financement = 4,
    Faisabilite = 5,
    Recevabilite = 6,
    ParcoursValide = 7,
    Accompagnement = 8,
    PreparationJury = 9,
    Jury = 10,
    PostJury = 11,
    Cloture = 12,
    Sortie = 99,
}

/// <summary>
/// Statut secondaire : se superpose à l'étape sans la faire avancer. Il indique
/// qui bloque le dossier, donc vers qui diriger l'action.
/// </summary>
public enum StatutSecondaire
{
    Aucun = 0,
    AttenteCandidat = 1,
    AttenteFranceVae = 2,
    AttenteFinanceur = 3,
    AttenteCertificateur = 4,
    Bloque = 5,
    Reoriente = 6,
}

public enum MotifSortie
{
    Aucun = 0,
    AbandonCandidat = 1,
    AbandonAap = 2,
    DisparuFranceVae = 3,
    CandidatureSupprimee = 4,
    Reorientation = 5,
    FinancementNonObtenu = 6,
}

public enum ResultatJury
{
    NonRenseigne = 0,
    ValidationTotale = 1,
    ValidationPartielle = 2,
    Refus = 3,
    Absence = 4,
}

/// <summary>Nature d'une séance. Détermine le tarif appliqué et le plafond opposable.</summary>
public enum NatureHeure
{
    Individuel = 1,
    Collectif = 2,
    ComplementFormatif = 3,
    /// <summary>Activité asynchrone EI Académie : tracée, mais non facturée à l'heure.</summary>
    Asynchrone = 4,
}

public enum DispositifFinancement
{
    NonSecurise = 0,
    Cpf = 1,
    Opco = 2,
    Employeur = 3,
    FranceTravail = 4,
    TransitionPro = 5,
    Region = 6,
    Autofinancement = 7,
    /// <summary>Contrat global d'entreprise, typique de la VAE collective.</summary>
    Entreprise = 8,
    Autre = 9,
}

public enum TypeIntervenant
{
    ArchitecteAccompagnateurParcours = 1,
    Accompagnateur = 2,
    ExpertMetier = 3,
    /// <summary>Coordination, administratif, financement, qualité, digital learning.</summary>
    Interne = 4,
}

public enum StatutIntervenant
{
    Actif = 1,
    EnIntegration = 2,
    Suspendu = 3,
    RetireDuReseau = 4,
}

public enum StatutCertificationInterne
{
    Active = 1,
    Suspendue = 2,
    NonCouverte = 3,
}

public enum SeveriteAlerte
{
    Information = 1,
    Vigilance = 2,
    Critique = 3,
}

public enum OrigineCandidature
{
    FranceVae = 1,
    SiteWeb = 2,
    EntreeDirecte = 3,
    Prescripteur = 4,
    VaeCollective = 5,
    Import = 6,
}

public enum SourceImport
{
    FranceCompetences = 1,
    FranceVaeApi = 2,
    FranceVaeExport = 3,
    SiteWeb = 4,
    Manuel = 5,
    ReprisExcel = 6,
}

public enum StatutImport
{
    EnCours = 1,
    Termine = 2,
    Echec = 3,
    TermineAvecErreurs = 4,
}

/// <summary>
/// Ce que vérifie une règle d'alerte. Chaque type est paramétré par la règle
/// elle-même (jalons, seuils, étapes) : on crée une règle sans toucher au code.
/// </summary>
public enum TypeCondition
{
    /// <summary>Délai écoulé depuis un jalon, éventuellement tant qu'un second jalon n'est pas atteint.</summary>
    DelaiDepuisJalon = 1,
    /// <summary>Jalon futur qui approche.</summary>
    EcheanceProche = 2,
    /// <summary>Aucun financement sécurisé.</summary>
    FinancementNonSecurise = 3,
    /// <summary>Aucune facture émise.</summary>
    FactureManquante = 4,
    /// <summary>Acteur non affecté : AAP, accompagnateur ou gestionnaire.</summary>
    ActeurManquant = 5,
    /// <summary>Part des heures prescrites déjà réalisées, en pourcentage.</summary>
    ConsommationHeures = 6,
    /// <summary>Dossier dans un statut secondaire donné.</summary>
    StatutSecondaire = 7,
    /// <summary>Consentement RGPD non recueilli.</summary>
    ConsentementManquant = 8,
}

/// <summary>Nature d'un produit du catalogue EI Académie.</summary>
public enum NatureModule
{
    /// <summary>Module e-learning suivi en autonomie sur l'espace candidat.</summary>
    ELearning = 1,
    /// <summary>Complément formatif, prescrit en heures et facturé à la grille.</summary>
    ComplementFormatif = 2,
}

/// <summary>Avancement d'un module inclus dans un parcours.</summary>
public enum StatutModule
{
    Prescrit = 1,
    EnCours = 2,
    Termine = 3,
    Abandonne = 4,
}

public enum ActeurDossier
{
    Aap = 1,
    Accompagnateur = 2,
    Gestionnaire = 3,
}

/// <summary>Événements donnant lieu à une notification.</summary>
public enum EvenementNotification
{
    CandidatCree = 1,
    AapAffecte = 2,
    AccompagnateurAffecte = 3,
    ParcoursPrescrit = 4,
    /// <summary>Recevabilité validée : le digital learning ouvre l'espace de formation.</summary>
    RecevabiliteValidee = 5,
    /// <summary>Recevabilité validée sans accompagnateur : la coordination doit en attribuer un.</summary>
    AccompagnateurAAttribuer = 6,
}
