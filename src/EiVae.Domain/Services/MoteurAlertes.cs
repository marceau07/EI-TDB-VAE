using System.Globalization;
using EiVae.Domain.Entities;

namespace EiVae.Domain.Services;

/// <summary>Une alerte levée sur un dossier.</summary>
public sealed record AlerteCalculee(RegleAlerte Regle, int ParcoursId, string Detail);

/// <summary>Date du parcours utilisable comme jalon d'une règle.</summary>
public sealed record JalonParcours(string Code, string Libelle, Func<Parcours, DateOnly?> Lire);

/// <summary>
/// Le cœur opérationnel. Il ne répond pas à « où en est le candidat ? » mais à
/// « qu'est-ce qui exige une action maintenant, de la part de qui, et au titre
/// de quelle règle ? ».
///
/// Les règles sont des données : le moteur ne connaît que les types de
/// condition, et chaque règle en fixe les paramètres. Il est pur et sans effet
/// de bord : il prend un parcours et une date de référence, et rend des alertes.
/// </summary>
public sealed class MoteurAlertes
{
    private readonly IReadOnlyList<RegleAlerte> _regles;
    private readonly TarificationService _tarification;

    public MoteurAlertes(IEnumerable<RegleAlerte> regles, TarificationService tarification)
    {
        _regles = regles.Where(r => r.Active).OrderBy(r => r.Ordre).ThenBy(r => r.Code).ToList();
        _tarification = tarification;
    }

    public IReadOnlyList<RegleAlerte> Regles => _regles;

    /// <summary>Dates du parcours que les règles peuvent viser, dans l'ordre du parcours.</summary>
    public static IReadOnlyList<JalonParcours> Jalons { get; } =
    [
        new(nameof(Parcours.DateDemande), "Demande reçue", p => p.DateDemande),
        new(nameof(Parcours.DatePremierContact), "Premier contact", p => p.DatePremierContact),
        new(nameof(Parcours.DateRecueilBesoins), "Recueil des besoins", p => p.DateRecueilBesoins),
        new(nameof(Parcours.DateRdvFaisabilite), "RDV faisabilité", p => p.DateRdvFaisabilite),
        new(nameof(Parcours.DateDepotFaisabilite), "Dépôt faisabilité", p => p.DateDepotFaisabilite),
        new(nameof(Parcours.DateRecevabilite), "Recevabilité", p => p.DateRecevabilite),
        new(nameof(Parcours.DateParcoursValide), "Parcours validé", p => p.DateParcoursValide),
        new(nameof(Parcours.DateDebutAccompagnement), "Début accompagnement", p => p.DateDebutAccompagnement),
        new(nameof(Parcours.DateDepotDossierValidation), "Dépôt dossier de validation", p => p.DateDepotDossierValidation),
        new(nameof(Parcours.DateJury), "Passage en jury", p => p.DateJury),
        new(nameof(Parcours.DateEntretienPostJury), "Entretien post-jury", p => p.DateEntretienPostJury),
        new(nameof(Parcours.DateDebutParcours), "Démarrage du parcours", p => p.DateDebutParcours),
        new(nameof(Parcours.DateDernierMouvement), "Dernier mouvement", p => p.DateDernierMouvement),
    ];

    private static readonly Dictionary<string, JalonParcours> JalonsParCode =
        Jalons.ToDictionary(j => j.Code, StringComparer.Ordinal);

    public static JalonParcours? Jalon(string? code) =>
        code is not null && JalonsParCode.TryGetValue(code, out var j) ? j : null;

    /// <summary>Règles livrées à l'installation. Elles se modifient ensuite depuis l'application.</summary>
    public static IReadOnlyList<RegleAlerte> ReglesParDefaut() =>
    [
        new()
        {
            Code = "R01", Ordre = 1, Severite = SeveriteAlerte.Critique,
            Libelle = "Premier rendez-vous pédagogique hors délai",
            Condition = TypeCondition.DelaiDepuisJalon,
            JalonReference = nameof(Parcours.DatePremierContact), JalonAttendu = nameof(Parcours.DateRecueilBesoins),
            Seuil = 8, JoursOuvres = true,
            ActionAttendue = "Planifier le rendez-vous pédagogique", Responsable = "AAP",
            Fondement = "Obligation France VAE",
        },
        new()
        {
            Code = "R02", Ordre = 2, Severite = SeveriteAlerte.Critique,
            Libelle = "Financement non sécurisé, parcours engagé",
            Condition = TypeCondition.FinancementNonSecurise,
            EtapeMin = EtapeParcours.Faisabilite, EtapeMax = EtapeParcours.PreparationJury,
            ActionAttendue = "Sécuriser le CPF, l'OPCO ou l'employeur", Responsable = "Pôle financement",
            Fondement = "Caisse des Dépôts — financement sécurisé avant validation du parcours",
        },
        new()
        {
            Code = "R03", Ordre = 3, Severite = SeveriteAlerte.Critique,
            Libelle = "Dossier sans évolution",
            Condition = TypeCondition.DelaiDepuisJalon,
            JalonReference = nameof(Parcours.DateDernierMouvement), Seuil = 45,
            ActionAttendue = "Reprendre contact et requalifier le dossier", Responsable = "AAP",
            Fondement = "Règle interne de pilotage",
        },
        new()
        {
            Code = "R04", Ordre = 4, Severite = SeveriteAlerte.Vigilance,
            Libelle = "Dossier ralenti",
            Condition = TypeCondition.DelaiDepuisJalon,
            JalonReference = nameof(Parcours.DateDernierMouvement), Seuil = 21, SeuilMax = 45,
            ActionAttendue = "Relancer le candidat", Responsable = "AAP",
            Fondement = "Règle interne de pilotage",
        },
        new()
        {
            Code = "R05", Ordre = 5, Severite = SeveriteAlerte.Critique,
            Libelle = "Recevabilité au-delà du délai réglementaire",
            Condition = TypeCondition.DelaiDepuisJalon,
            JalonReference = nameof(Parcours.DateDepotFaisabilite), JalonAttendu = nameof(Parcours.DateRecevabilite),
            Seuil = 60,
            ActionAttendue = "Relancer le certificateur", Responsable = "Coordination",
            Fondement = "Délai réglementaire — 2 mois",
        },
        new()
        {
            Code = "R06", Ordre = 6, Severite = SeveriteAlerte.Critique,
            Libelle = "Jury non organisé après dépôt du dossier",
            Condition = TypeCondition.DelaiDepuisJalon,
            JalonReference = nameof(Parcours.DateDepotDossierValidation), JalonAttendu = nameof(Parcours.DateJury),
            Seuil = 90,
            ActionAttendue = "Relancer le certificateur pour obtenir une date", Responsable = "Coordination",
            Fondement = "Délai réglementaire — 3 mois",
        },
        new()
        {
            Code = "R07", Ordre = 7, Severite = SeveriteAlerte.Vigilance,
            Libelle = "Échéance de jury proche",
            Condition = TypeCondition.EcheanceProche,
            JalonReference = nameof(Parcours.DateJury), Seuil = 30,
            ActionAttendue = "Lancer la préparation à l'oral", Responsable = "Accompagnateur",
            Fondement = "Règle interne de pilotage",
        },
        new()
        {
            Code = "R08", Ordre = 8, Severite = SeveriteAlerte.Vigilance,
            Libelle = "Enveloppe d'heures presque consommée",
            Condition = TypeCondition.ConsommationHeures, Seuil = 85,
            ActionAttendue = "Arbitrer entre un avenant et la clôture", Responsable = "Accompagnateur",
            Fondement = "Cohérence devis / prestations",
        },
        new()
        {
            Code = "R09", Ordre = 9, Severite = SeveriteAlerte.Critique,
            Libelle = "Parcours au-delà de la durée cible",
            Condition = TypeCondition.DelaiDepuisJalon,
            JalonReference = nameof(Parcours.DateDemande), Seuil = 365,
            ActionAttendue = "Arbitrer : accélérer, réorienter ou clôturer", Responsable = "Responsable VAE",
            Fondement = "Objectif national — 6 à 8 mois",
        },
        new()
        {
            Code = "R10", Ordre = 10, Severite = SeveriteAlerte.Vigilance,
            Libelle = "Parcours long",
            Condition = TypeCondition.DelaiDepuisJalon,
            JalonReference = nameof(Parcours.DateDemande), Seuil = 240, SeuilMax = 365,
            ActionAttendue = "Resserrer le calendrier", Responsable = "AAP",
            Fondement = "Objectif national — 6 à 8 mois",
        },
        new()
        {
            Code = "R11", Ordre = 11, Severite = SeveriteAlerte.Vigilance,
            Libelle = "Risque d'abandon",
            Condition = TypeCondition.DelaiDepuisJalon,
            JalonReference = nameof(Parcours.DateDemande), JalonAttendu = nameof(Parcours.DateRecueilBesoins),
            Seuil = 60,
            ActionAttendue = "Dernier contact, puis décision de sortie", Responsable = "AAP",
            Fondement = "Règle interne de pilotage",
        },
        new()
        {
            Code = "R12", Ordre = 12, Severite = SeveriteAlerte.Critique,
            Libelle = "Prestation réalisée non facturée",
            Condition = TypeCondition.FactureManquante,
            EtapeMin = EtapeParcours.PostJury, EtapeMax = EtapeParcours.PostJury,
            ActionAttendue = "Émettre la facture de fin de parcours", Responsable = "Pôle administratif",
            Fondement = "Règle interne de pilotage",
        },
        new()
        {
            Code = "R13", Ordre = 13, Severite = SeveriteAlerte.Vigilance,
            Libelle = "Accompagnement sans accompagnateur affecté",
            Condition = TypeCondition.ActeurManquant, Acteur = ActeurDossier.Accompagnateur,
            EtapeMin = EtapeParcours.Accompagnement, EtapeMax = EtapeParcours.Jury,
            ActionAttendue = "Affecter un accompagnateur habilité", Responsable = "Coordination pédagogique",
            Fondement = "Règle interne de pilotage",
        },
        new()
        {
            Code = "R14", Ordre = 14, Severite = SeveriteAlerte.Vigilance,
            Libelle = "Dossier sans AAP référent",
            Condition = TypeCondition.ActeurManquant, Acteur = ActeurDossier.Aap,
            ActionAttendue = "Désigner l'AAP référent", Responsable = "Responsable VAE",
            Fondement = "Règle interne de pilotage",
        },
        new()
        {
            Code = "R15", Ordre = 15, Severite = SeveriteAlerte.Vigilance,
            Libelle = "Dossier en attente du candidat",
            Condition = TypeCondition.StatutSecondaire, Statut = StatutSecondaire.AttenteCandidat,
            ActionAttendue = "Relancer le candidat, puis décision de sortie", Responsable = "AAP",
            Fondement = "Règle interne de pilotage",
        },
    ];

    /// <summary>Évalue toutes les règles actives sur un dossier.</summary>
    public IReadOnlyList<AlerteCalculee> Evaluer(Parcours p, DateOnly aujourdHui)
    {
        if (!p.EstActif)
        {
            return [];
        }

        var alertes = new List<AlerteCalculee>();
        foreach (var regle in _regles)
        {
            if (regle.EtapeMin is { } min && p.Etape < min)
            {
                continue;
            }

            if (regle.EtapeMax is { } max && p.Etape > max)
            {
                continue;
            }

            if (Verifier(regle, p, aujourdHui) is { } detail)
            {
                alertes.Add(new AlerteCalculee(regle, p.Id, detail));
            }
        }

        return alertes;
    }

    /// <summary>Rend le détail de l'alerte si la condition est vérifiée, null sinon.</summary>
    private string? Verifier(RegleAlerte r, Parcours p, DateOnly aujourdHui)
    {
        switch (r.Condition)
        {
            case TypeCondition.DelaiDepuisJalon:
            {
                var reference = Jalon(r.JalonReference);
                if (reference?.Lire(p) is not { } depuis || r.Seuil is not { } seuil)
                {
                    return null;
                }

                if (Jalon(r.JalonAttendu) is { } attendu && attendu.Lire(p) is not null)
                {
                    return null;
                }

                var ecart = r.JoursOuvres
                    ? JoursOuvres(depuis, aujourdHui)
                    : aujourdHui.DayNumber - depuis.DayNumber;

                if (ecart <= seuil || (r.SeuilMax is { } plafond && ecart > plafond))
                {
                    return null;
                }

                // Au-delà de deux mois, le mois parle mieux que le jour.
                var duree = !r.JoursOuvres && ecart > 60 && reference.Code == nameof(Parcours.DateDemande)
                    ? $"{ecart / 30} mois"
                    : $"{ecart} jours{(r.JoursOuvres ? " ouvrés" : string.Empty)}";

                return reference.Code == nameof(Parcours.DateDernierMouvement)
                    ? $"{duree} sans mouvement"
                    : $"{duree} depuis : {reference.Libelle.ToLower(CultureInfo.GetCultureInfo("fr-FR"))}";
            }

            case TypeCondition.EcheanceProche:
            {
                if (Jalon(r.JalonReference) is not { } jalon || jalon.Lire(p) is not { } date
                    || date <= aujourdHui || r.Seuil is not { } seuil)
                {
                    return null;
                }

                var restants = date.DayNumber - aujourdHui.DayNumber;
                return restants <= seuil
                    ? $"{jalon.Libelle} le {date:dd/MM/yyyy}, dans {restants} jours"
                    : null;
            }

            case TypeCondition.FinancementNonSecurise:
                return p.Financements.Any(f => f.EstSecurise)
                    ? null
                    : $"étape « {Libelle(p.Etape)} » sans prise en charge sécurisée";

            case TypeCondition.FactureManquante:
                return p.Factures.Count > 0
                    ? null
                    : $"{_tarification.Calculer(p).Total:0} € à facturer";

            case TypeCondition.ActeurManquant:
                return r.Acteur switch
                {
                    ActeurDossier.Aap when p.AapId is null => "aucun AAP renseigné",
                    ActeurDossier.Accompagnateur when p.AccompagnateurId is null =>
                        $"certification {p.Certification?.Abrege ?? p.Certification?.Intitule ?? "non renseignée"}",
                    ActeurDossier.Gestionnaire when p.GestionnaireId is null => "aucun gestionnaire renseigné",
                    _ => null,
                };

            case TypeCondition.ConsommationHeures:
            {
                if (p.HeuresPrescritesTotales <= 0 || r.Seuil is not { } seuil)
                {
                    return null;
                }

                var realisees = p.Seances.Where(s => s.Realisee && s.Nature != NatureHeure.Asynchrone)
                                         .Sum(s => s.DureeHeures);
                var taux = realisees / p.HeuresPrescritesTotales;
                return taux * 100 >= seuil
                    ? $"{realisees:0.#} h réalisées sur {p.HeuresPrescritesTotales:0.#} h prescrites ({taux:P0})"
                    : null;
            }

            case TypeCondition.StatutSecondaire:
                return r.Statut is { } statut && p.StatutSecondaire == statut
                    ? $"statut « {LibelleStatut(statut)} »"
                    : null;

            case TypeCondition.ConsentementManquant:
                return p.Candidat is { ConsentementRgpd: false } ? "consentement RGPD non recueilli" : null;

            default:
                return null;
        }
    }

    /// <summary>Formulation lisible de ce que vérifie une règle, construite à partir de ses paramètres.</summary>
    public static string DecrireSeuil(RegleAlerte r)
    {
        var unite = r.JoursOuvres ? "jours ouvrés" : "jours";
        var reference = Jalon(r.JalonReference)?.Libelle.ToLower(CultureInfo.GetCultureInfo("fr-FR")) ?? "?";
        var attendu = Jalon(r.JalonAttendu)?.Libelle.ToLower(CultureInfo.GetCultureInfo("fr-FR"));

        var coeur = r.Condition switch
        {
            TypeCondition.DelaiDepuisJalon =>
                (r.SeuilMax is { } max
                    ? $"entre {r.Seuil} et {max} {unite} depuis : {reference}"
                    : $"plus de {r.Seuil} {unite} depuis : {reference}")
                + (attendu is null ? string.Empty : $", sans : {attendu}"),
            TypeCondition.EcheanceProche => $"{reference} dans moins de {r.Seuil} jours",
            TypeCondition.FinancementNonSecurise => "aucune prise en charge sécurisée",
            TypeCondition.FactureManquante => "aucune facture émise",
            TypeCondition.ActeurManquant => r.Acteur switch
            {
                ActeurDossier.Aap => "aucun AAP référent",
                ActeurDossier.Accompagnateur => "aucun accompagnateur affecté",
                ActeurDossier.Gestionnaire => "aucun gestionnaire affecté",
                _ => "acteur non précisé",
            },
            TypeCondition.ConsommationHeures => $"au moins {r.Seuil} % des heures prescrites réalisées",
            TypeCondition.StatutSecondaire => r.Statut is { } s ? $"statut « {LibelleStatut(s)} »" : "statut non précisé",
            TypeCondition.ConsentementManquant => "consentement RGPD non recueilli",
            _ => r.Condition.ToString(),
        };

        var etapes = (r.EtapeMin, r.EtapeMax) switch
        {
            ({ } a, { } b) when a == b => $" — étape {Libelle(a)}",
            ({ } a, { } b) => $" — étapes {Libelle(a)} à {Libelle(b)}",
            ({ } a, null) => $" — à partir de l'étape {Libelle(a)}",
            (null, { } b) => $" — jusqu'à l'étape {Libelle(b)}",
            _ => string.Empty,
        };

        return coeur + etapes;
    }

    /// <summary>
    /// Jours ouvrés entre deux dates, samedis et dimanches exclus. Les jours
    /// fériés ne sont pas déduits : France VAE raisonne en jours ouvrés simples.
    /// </summary>
    public static int JoursOuvres(DateOnly debut, DateOnly fin)
    {
        if (fin <= debut)
        {
            return 0;
        }

        var total = fin.DayNumber - debut.DayNumber;
        var semaines = total / 7;
        var reste = total % 7;
        var jours = semaines * 5;

        var jour = debut.DayOfWeek;
        for (var i = 0; i < reste; i++)
        {
            jour = jour == DayOfWeek.Saturday ? DayOfWeek.Sunday : jour + 1;
            if (jour is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
            {
                jours++;
            }
        }

        return jours;
    }

    public static string Libelle(EtapeParcours e) => e switch
    {
        EtapeParcours.DemandeRecue => "Demande reçue",
        EtapeParcours.Qualification => "Qualification",
        EtapeParcours.RecueilDesBesoins => "Recueil des besoins",
        EtapeParcours.Financement => "Financement",
        EtapeParcours.Faisabilite => "Faisabilité",
        EtapeParcours.Recevabilite => "Recevabilité validée",
        EtapeParcours.ParcoursValide => "Parcours validé",
        EtapeParcours.Accompagnement => "Accompagnement",
        EtapeParcours.PreparationJury => "Préparation jury",
        EtapeParcours.Jury => "Jury",
        EtapeParcours.PostJury => "Post-jury",
        EtapeParcours.Cloture => "Clôturé",
        EtapeParcours.Sortie => "Sorti du parcours",
        _ => e.ToString(),
    };

    public static string LibelleStatut(StatutSecondaire s) => s switch
    {
        StatutSecondaire.AttenteCandidat => "En attente candidat",
        StatutSecondaire.AttenteFranceVae => "En attente France VAE",
        StatutSecondaire.AttenteFinanceur => "En attente financeur",
        StatutSecondaire.AttenteCertificateur => "En attente certificateur",
        StatutSecondaire.Bloque => "Bloqué",
        StatutSecondaire.Reoriente => "Réorienté",
        _ => string.Empty,
    };

    public static string LibelleCondition(TypeCondition c) => c switch
    {
        TypeCondition.DelaiDepuisJalon => "Délai depuis un jalon",
        TypeCondition.EcheanceProche => "Échéance proche",
        TypeCondition.FinancementNonSecurise => "Financement non sécurisé",
        TypeCondition.FactureManquante => "Aucune facture émise",
        TypeCondition.ActeurManquant => "Acteur non affecté",
        TypeCondition.ConsommationHeures => "Consommation des heures",
        TypeCondition.StatutSecondaire => "Statut secondaire",
        TypeCondition.ConsentementManquant => "Consentement RGPD manquant",
        _ => c.ToString(),
    };
}
