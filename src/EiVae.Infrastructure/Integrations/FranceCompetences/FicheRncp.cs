using System.Globalization;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;

namespace EiVae.Infrastructure.Integrations.FranceCompetences;

/// <summary>Un bloc de compétences tel que publié dans l'export.</summary>
public sealed record BlocRncp(string Code, string Libelle, string? Competences, string? ModalitesEvaluation);

/// <summary>Un organisme certificateur tel que publié dans l'export.</summary>
public sealed record CertificateurRncp(string Nom, string? Siret, string? Etat);

/// <summary>
/// Projection d'une fiche RNCP de l'export officiel v4.1.
///
/// La lecture est faite nœud par nœud sur un <see cref="XmlReader"/> positionné
/// sur l'élément FICHE : le sous-arbre d'une fiche pèse quelques dizaines de
/// kilo-octets, alors que le document complet en pèse près de 500 000.
/// </summary>
public sealed class FicheRncp
{
    public required string CodeRncp { get; init; }
    public string? IdFiche { get; init; }
    public required string Intitule { get; init; }
    public string? EtatFiche { get; init; }
    public bool Actif { get; init; }
    public int? Niveau { get; init; }
    public string? LibelleNiveau { get; init; }
    public string? TypeEnregistrement { get; init; }

    public DateOnly? DateDecision { get; init; }
    public DateOnly? DateFinEnregistrement { get; init; }
    public DateOnly? DateLimiteDelivrance { get; init; }
    public DateOnly? DateDerniereModification { get; init; }

    public bool VoieVae { get; init; }
    public string? CompositionJuryVae { get; init; }
    public bool VoieFormationInitiale { get; init; }
    public bool VoieFormationContinue { get; init; }
    public bool VoieApprentissage { get; init; }
    public bool VoieContratProfessionnalisation { get; init; }
    public bool VoieCandidatLibre { get; init; }

    public string? ActivitesVisees { get; init; }
    public string? CapacitesAttestees { get; init; }
    public string? SecteursActivite { get; init; }
    public string? TypeEmploiAccessibles { get; init; }
    public string? ObjectifsContexte { get; init; }
    public string? Prerequis { get; init; }
    public string? ReglementationActivites { get; init; }

    public string? CodesNsfJson { get; init; }
    public string? FormacodesJson { get; init; }
    public string? CodesRomeJson { get; init; }
    public string? StatistiquesJson { get; init; }

    public IReadOnlyList<BlocRncp> Blocs { get; init; } = [];
    public IReadOnlyList<CertificateurRncp> Certificateurs { get; init; } = [];

    public string? DomaineDeduit { get; init; }

    /// <summary>
    /// Lit la fiche sur laquelle le lecteur est positionné. Rend null si la fiche
    /// est inexploitable — code ou intitulé manquant.
    /// </summary>
    public static FicheRncp? Lire(XmlReader lecteur)
    {
        var element = XNode.ReadFrom(lecteur) as XElement;
        if (element is null)
        {
            return null;
        }

        var code = Texte(element, "NUMERO_FICHE");
        var intitule = Texte(element, "INTITULE");
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(intitule))
        {
            return null;
        }

        var etat = Texte(element, "ETAT_FICHE");
        var fin = Date(element, "DATE_FIN_ENREGISTREMENT");
        var aujourdHui = DateOnly.FromDateTime(DateTime.UtcNow);

        var nsf = element.Element("CODES_NSF")?.Elements("NSF")
            .Select(n => new { code = Texte(n, "CODE"), libelle = Texte(n, "LIBELLE") }).ToList() ?? [];

        var blocs = element.Element("BLOCS_COMPETENCES")?.Elements("BLOC_COMPETENCES")
            .Select(bl => new BlocRncp(
                Texte(bl, "CODE") ?? string.Empty,
                Texte(bl, "LIBELLE") ?? string.Empty,
                Texte(bl, "LISTE_COMPETENCES"),
                Texte(bl, "MODALITES_EVALUATION")))
            .Where(bl => !string.IsNullOrWhiteSpace(bl.Code))
            .ToList() ?? [];

        var certificateurs = element.Element("CERTIFICATEURS")?.Elements("CERTIFICATEUR")
            .Select(c => new CertificateurRncp(
                Texte(c, "NOM_CERTIFICATEUR") ?? string.Empty,
                Texte(c, "SIRET_CERTIFICATEUR"),
                Texte(c, "ETAT_CERTIFICATEUR")))
            .Where(c => !string.IsNullOrWhiteSpace(c.Nom))
            .ToList() ?? [];

        var stats = element.Element("STATISTIQUES_PROMOTIONS")?.Elements()
            .Select(s => new
            {
                annee = Texte(s, "ANNEE"),
                certifies = Texte(s, "NOMBRE_CERTIFIES"),
                certifiesVae = Texte(s, "NOMBRE_CERTIFIES_VAE"),
                insertion6Mois = Texte(s, "TAUX_INSERTION_GLOBAL_6MOIS"),
                insertion2Ans = Texte(s, "TAUX_INSERTION_METIER_2ANS"),
            })
            .Where(s => !string.IsNullOrWhiteSpace(s.annee))
            .ToList() ?? [];

        return new FicheRncp
        {
            CodeRncp = code,
            IdFiche = Texte(element, "ID_FICHE"),
            Intitule = intitule,
            EtatFiche = etat,
            Actif = (etat?.StartsWith("Publi", StringComparison.OrdinalIgnoreCase) ?? false)
                    && (fin is null || fin >= aujourdHui),
            Niveau = NiveauNumerique(Texte(element, "NOMENCLATURE_EUROPE", "NIVEAU")),
            LibelleNiveau = Texte(element, "NOMENCLATURE_EUROPE", "LIBELLE"),
            TypeEnregistrement = Texte(element, "TYPE_ENREGISTREMENT"),
            DateDecision = Date(element, "DATE_DECISION"),
            DateFinEnregistrement = fin,
            DateLimiteDelivrance = Date(element, "DATE_LIMITE_DELIVRANCE"),
            DateDerniereModification = Date(element, "DATE_DERNIERE_MODIFICATION"),

            // Chaque SI_JURY_* est un conteneur portant ACTIF (Oui/Non) et, quand
            // la voie est ouverte, COMPOSITION : la composition du jury.
            VoieVae = Oui(element, "SI_JURY_VAE", "ACTIF"),
            CompositionJuryVae = Texte(element, "SI_JURY_VAE", "COMPOSITION"),
            VoieFormationInitiale = Oui(element, "SI_JURY_FI", "ACTIF"),
            VoieFormationContinue = Oui(element, "SI_JURY_FC", "ACTIF"),
            VoieApprentissage = Oui(element, "SI_JURY_CA", "ACTIF"),
            VoieContratProfessionnalisation = Oui(element, "SI_JURY_CQ", "ACTIF"),
            VoieCandidatLibre = Oui(element, "SI_JURY_CL", "ACTIF"),

            ActivitesVisees = Texte(element, "ACTIVITES_VISEES"),
            CapacitesAttestees = Texte(element, "CAPACITES_ATTESTEES"),
            SecteursActivite = Texte(element, "SECTEURS_ACTIVITE"),
            TypeEmploiAccessibles = Texte(element, "TYPE_EMPLOI_ACCESSIBLES"),
            ObjectifsContexte = Texte(element, "OBJECTIFS_CONTEXTE"),
            Prerequis = Texte(element, "PREREQUIS_ENTREE_FORMATION"),
            ReglementationActivites = Texte(element, "REGLEMENTATIONS_ACTIVITES"),

            CodesNsfJson = nsf.Count == 0 ? null : JsonSerializer.Serialize(nsf),
            FormacodesJson = Json(element, "FORMACODES", "FORMACODE"),
            CodesRomeJson = Json(element, "CODES_ROME", "ROME"),
            StatistiquesJson = stats.Count == 0 ? null : JsonSerializer.Serialize(stats),

            Blocs = blocs,
            Certificateurs = certificateurs,
            DomaineDeduit = ClassificationDomaine.Deduire(intitule, nsf.Select(n => n.code)),
        };
    }

    private static string? Texte(XElement parent, params string[] chemin)
    {
        XElement? courant = parent;
        foreach (var nom in chemin)
        {
            courant = courant?.Element(nom);
            if (courant is null)
            {
                return null;
            }
        }

        var valeur = courant.Value?.Trim();
        return string.IsNullOrWhiteSpace(valeur) ? null : Normaliser(valeur);
    }

    private static string Normaliser(string v)
    {
        // L'export contient de longues suites d'espaces issues des saisies HTML.
        Span<char> tampon = v.Length <= 512 ? stackalloc char[v.Length] : new char[v.Length];
        var n = 0;
        var espacePrecedent = false;

        foreach (var c in v)
        {
            var estEspace = c is ' ' or '\t';
            if (estEspace && espacePrecedent)
            {
                continue;
            }

            tampon[n++] = estEspace ? ' ' : c;
            espacePrecedent = estEspace;
        }

        return new string(tampon[..n]).Trim();
    }

    private static bool Oui(XElement parent, params string[] chemin) =>
        string.Equals(Texte(parent, chemin), "Oui", StringComparison.OrdinalIgnoreCase);

    private static string? Json(XElement parent, string conteneur, string enfant)
    {
        var items = parent.Element(conteneur)?.Elements(enfant)
            .Select(n => new { code = Texte(n, "CODE"), libelle = Texte(n, "LIBELLE") })
            .Where(n => n.code is not null)
            .ToList();

        return items is null || items.Count == 0 ? null : JsonSerializer.Serialize(items);
    }

    private static int? NiveauNumerique(string? niveau)
    {
        if (string.IsNullOrWhiteSpace(niveau))
        {
            return null;
        }

        foreach (var c in niveau)
        {
            if (char.IsDigit(c))
            {
                return c - '0';
            }
        }

        return null;
    }

    /// <summary>Les dates de l'export sont au format jj/mm/aaaa.</summary>
    private static DateOnly? Date(XElement parent, params string[] chemin)
    {
        var v = Texte(parent, chemin);
        if (string.IsNullOrWhiteSpace(v))
        {
            return null;
        }

        string[] formats = ["dd/MM/yyyy", "yyyy-MM-dd", "dd-MM-yyyy"];
        return DateOnly.TryParseExact(v, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d
            : null;
    }
}
