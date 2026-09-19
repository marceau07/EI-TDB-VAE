namespace EiVae.Infrastructure.Integrations.FranceCompetences;

/// <summary>
/// Rattache une certification à un domaine métier EI Groupe.
///
/// La nomenclature NSF de France Compétences est trop fine pour le pilotage du
/// service : elle distingue des spécialités que les équipes traitent ensemble.
/// On classe donc d'abord sur l'intitulé — porteur du vocabulaire réellement
/// employé — puis on retombe sur le code NSF.
///
/// Cette classification n'est appliquée qu'à la création d'une certification :
/// une reclassification manuelle par le service n'est jamais écrasée.
/// </summary>
public static class ClassificationDomaine
{
    public const string PetiteEnfance = "Petite enfance";
    public const string SanitaireSocial = "Sanitaire & social";
    public const string ServiceALaPersonne = "Service à la personne";
    public const string FormationRh = "Formation & RH";
    public const string LogistiqueTransport = "Logistique & transport";
    public const string Commerce = "Commerce";
    public const string Securite = "Sécurité";
    public const string PropreteEnvironnement = "Propreté & environnement";
    public const string AnimationCulture = "Animation & culture";
    public const string Agriculture = "Agriculture";
    public const string Autres = "Autres";

    public static IReadOnlyList<string> Tous { get; } =
    [
        PetiteEnfance, SanitaireSocial, ServiceALaPersonne, FormationRh, LogistiqueTransport,
        Commerce, Securite, PropreteEnvironnement, AnimationCulture, Agriculture, Autres,
    ];

    /// <summary>
    /// Ordre significatif : la première correspondance gagne. Les domaines les
    /// plus spécifiques passent avant les plus généraux, sans quoi « éducateur de
    /// jeunes enfants » serait classé en sanitaire et social plutôt qu'en petite
    /// enfance.
    /// </summary>
    private static readonly (string Domaine, string[] Mots)[] Regles =
    [
        (PetiteEnfance, [
            "petite enfance", "jeunes enfants", "éducatif petite", "puéricult", "périscolaire",
            "vie scolaire", "ludothé", "crèche", "assistant maternel",
        ]),
        (ServiceALaPersonne, [
            "domicile", "vie aux familles", "grand âge", "auxiliaire de vie", "aide à domicile",
            "services au domicile",
        ]),
        (SanitaireSocial, [
            "social", "médico", "aide-soignant", "soins", "éducateur", "educateur", "familial",
            "familiale", "caferuis", "gérontolog", "médiation", "médiateur", "insertion",
            "orthopédago", "handicap", "assistant de service", "économie sociale",
            "intervention sociale", "moniteur", "surveillant de nuit", "maître de maison",
        ]),
        (FormationRh, [
            "formateur", "formation", "pédagog", "ressources humaines", "compétences",
            "management", "ingénierie", "consultant", "développement rh",
        ]),
        (LogistiqueTransport, [
            "logisti", "transport", "magasinier", "magasin", "entrepos", "préparateur de commandes",
            "déménag", "chaine logistique", "chaîne logistique", "traction", "attelage",
        ]),
        (Securite, [
            "sécurité", "sûreté", "télésurveillance", "vidéoprotection", "prévention et de sécurité",
            "gardien", "concierge", "agent de prévention", "incendie",
        ]),
        (PropreteEnvironnement, [
            "propreté", "hygiène", "nettoyage", "laveur de vitres", "environnement",
            "biocontamination", "nuisible", "réemploi", "valoriste", "stérilisation", "machiniste",
        ]),
        (Commerce, [
            "commerce", "commercial", "vente", "relation client", "négoce", "esthétique",
            "esthéticien", "thermal", "distribution",
        ]),
        (AnimationCulture, [
            "animation", "animateur", "esport", "sportive", "artistique", "culturel", "pastorale",
            "multimédia", "graphique", "motion", "designer", "numérique", "communication",
        ]),
        (Agriculture, [
            "agricole", "agrofourniture", "équine", "rural", "grains",
        ]),
    ];

    /// <summary>
    /// Regroupement des codes NSF, utilisé quand l'intitulé ne tranche pas.
    /// Référence : Nomenclature des spécialités de formation, groupes 3xx.
    /// </summary>
    private static readonly Dictionary<string, string> ParNsf = new(StringComparer.Ordinal)
    {
        ["310"] = FormationRh,
        ["311"] = FormationRh,
        ["312"] = Commerce,
        ["313"] = FormationRh,
        ["314"] = FormationRh,
        ["315"] = FormationRh,
        ["320"] = AnimationCulture,
        ["321"] = AnimationCulture,
        ["322"] = AnimationCulture,
        ["323"] = AnimationCulture,
        ["324"] = FormationRh,
        ["330"] = SanitaireSocial,
        ["331"] = SanitaireSocial,
        ["332"] = SanitaireSocial,
        ["333"] = PetiteEnfance,
        ["334"] = Commerce,
        ["335"] = AnimationCulture,
        ["336"] = Commerce,
        ["343"] = PropreteEnvironnement,
        ["344"] = Securite,
        ["345"] = FormationRh,
        ["346"] = PropreteEnvironnement,
    };

    public static string Deduire(string? intitule, IEnumerable<string?> codesNsf)
    {
        var libelle = (intitule ?? string.Empty).ToLowerInvariant();

        foreach (var (domaine, mots) in Regles)
        {
            foreach (var mot in mots)
            {
                if (libelle.Contains(mot, StringComparison.Ordinal))
                {
                    return domaine;
                }
            }
        }

        foreach (var code in codesNsf)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                continue;
            }

            // Les codes NSF sont de la forme « 332 » ou « 332t » : on ne retient
            // que le groupe à trois chiffres.
            var groupe = new string(code.Where(char.IsDigit).Take(3).ToArray());
            if (groupe.Length == 3 && ParNsf.TryGetValue(groupe, out var domaine))
            {
                return domaine;
            }
        }

        return Autres;
    }
}
