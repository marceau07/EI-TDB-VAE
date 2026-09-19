using System.Globalization;
using System.Text;
using EiVae.Domain.Entities;

namespace EiVae.Domain.Services;

/// <summary>Configuration du site SharePoint hébergeant les dossiers du service VAE.</summary>
public sealed class SharePointOptions
{
    public const string Section = "SharePoint";

    /// <summary>Racine du site, sans barre oblique finale. Exemple : https://eigroupe.sharepoint.com/sites/VAE</summary>
    public string SiteUrl { get; set; } = string.Empty;

    /// <summary>Nom de la bibliothèque de documents. Exemple : Documents partages</summary>
    public string Bibliotheque { get; set; } = "Documents partages";

    /// <summary>Dossier racine des dossiers candidats à l'intérieur de la bibliothèque.</summary>
    public string RacineCandidats { get; set; } = "Candidats";

    /// <summary>Dossier racine des dossiers RH des intervenants.</summary>
    public string RacineIntervenants { get; set; } = "Reseau VAE";

    /// <summary>Dossier racine des projets de VAE collective.</summary>
    public string RacineProjets { get; set; } = "VAE collective";

    /// <summary>
    /// Gabarit de nommage d'un dossier candidat. Jetons disponibles :
    /// {annee} {nom} {prenom} {parcoursId} {certification} {acf}
    /// </summary>
    public string GabaritDossierCandidat { get; set; } = "{nom}_{prenom}_{certification}";

    public bool EstConfigure => !string.IsNullOrWhiteSpace(SiteUrl);
}

/// <summary>
/// Construit les liens vers SharePoint. L'application ne lit ni n'écrit dans
/// SharePoint : elle stocke le chemin relatif de chaque dossier et sait
/// reconstruire une URL ouvrable. Le site peut donc être déplacé ou renommé sans
/// invalider les données.
///
/// Le gabarit de nommage est configurable pour épouser l'arborescence réelle
/// décidée lors de la migration, plutôt que d'imposer la nôtre.
/// </summary>
public sealed class SharePointLinkBuilder
{
    private readonly SharePointOptions _options;

    public SharePointLinkBuilder(SharePointOptions options) => _options = options;

    public bool EstConfigure => _options.EstConfigure;

    /// <summary>Chemin relatif normalisé du dossier d'un candidat, selon le gabarit configuré.</summary>
    public string CheminDossierCandidat(Parcours parcours)
    {
        if (!string.IsNullOrWhiteSpace(parcours.SharePointDossier))
        {
            return parcours.SharePointDossier;
        }

        var annee = (parcours.DateDemande ?? parcours.DateDebutParcours
                     ?? DateOnly.FromDateTime(DateTime.UtcNow)).Year.ToString(CultureInfo.InvariantCulture);

        var chemin = _options.GabaritDossierCandidat
            .Replace("{annee}", annee, StringComparison.Ordinal)
            .Replace("{nom}", Nettoyer(parcours.Candidat?.Nom), StringComparison.Ordinal)
            .Replace("{prenom}", Nettoyer(parcours.Candidat?.Prenom), StringComparison.Ordinal)
            .Replace("{parcoursId}", parcours.Id.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{certification}", NomCertification(parcours.Certification), StringComparison.Ordinal)
            .Replace("{acf}", Nettoyer(parcours.CodeAcfSolei), StringComparison.Ordinal);

        return $"{_options.RacineCandidats}/{NettoyerChemin(chemin)}";
    }

    /// <summary>
    /// Abrégé de la certification, à défaut son intitulé tronqué : un intitulé
    /// complet dépasse vite la longueur de chemin que tolère SharePoint.
    /// </summary>
    private static string NomCertification(Certification? c)
    {
        var nom = Nettoyer(string.IsNullOrWhiteSpace(c?.Abrege) ? c?.Intitule : c.Abrege);
        return nom.Length <= 60 ? nom : nom[..60].TrimEnd(' ', '-', '.');
    }

    /// <summary>URL ouvrable du dossier d'un candidat, ou null si SharePoint n'est pas configuré.</summary>
    public string? UrlDossierCandidat(Parcours parcours) =>
        UrlDepuisChemin(CheminDossierCandidat(parcours));

    public string? UrlDossierIntervenant(Intervenant intervenant)
    {
        var chemin = string.IsNullOrWhiteSpace(intervenant.SharePointDossier)
            ? $"{_options.RacineIntervenants}/{Nettoyer(intervenant.Nom)} {Nettoyer(intervenant.Prenom)}".Trim()
            : intervenant.SharePointDossier;

        return UrlDepuisChemin(chemin);
    }

    public string? UrlDossierProjet(ProjetCollectif projet)
    {
        var chemin = string.IsNullOrWhiteSpace(projet.SharePointDossier)
            ? $"{_options.RacineProjets}/{Nettoyer(projet.RaisonSociale)} - {Nettoyer(projet.Nom)}"
            : projet.SharePointDossier;

        return UrlDepuisChemin(chemin);
    }

    /// <summary>URL d'une pièce précise à l'intérieur du dossier d'un candidat.</summary>
    public string? UrlPiece(Parcours parcours, PieceDossier piece)
    {
        if (!_options.EstConfigure)
        {
            return null;
        }

        var chemin = string.IsNullOrWhiteSpace(piece.CheminSharePoint)
            ? CheminDossierCandidat(parcours)
            : $"{CheminDossierCandidat(parcours)}/{piece.CheminSharePoint}";

        return UrlDepuisChemin(chemin);
    }

    /// <summary>
    /// Lien d'ouverture d'un dossier dans l'explorateur SharePoint. On passe par
    /// la vue « Forms/AllItems.aspx?id= », qui ouvre le dossier ciblé plutôt que
    /// la racine de la bibliothèque.
    /// </summary>
    public string? UrlDepuisChemin(string? cheminRelatif)
    {
        if (!_options.EstConfigure || string.IsNullOrWhiteSpace(cheminRelatif))
        {
            return null;
        }

        var site = _options.SiteUrl.TrimEnd('/');
        var chemin = cheminRelatif.Trim('/');

        // Chemin absolu serveur attendu par SharePoint : /sites/VAE/Documents partages/...
        var cheminServeur = $"{CheminServeurDuSite(site)}/{_options.Bibliotheque.Trim('/')}/{chemin}";

        var bibliotheque = Uri.EscapeDataString(_options.Bibliotheque.Trim('/'));
        return $"{site}/{bibliotheque}/Forms/AllItems.aspx?id={Uri.EscapeDataString(cheminServeur)}"
               + $"&viewid=root&parent={Uri.EscapeDataString(CheminParent(cheminServeur))}";
    }

    private static string CheminParent(string chemin)
    {
        var i = chemin.LastIndexOf('/');
        return i <= 0 ? chemin : chemin[..i];
    }

    private static string CheminServeurDuSite(string siteUrl) =>
        Uri.TryCreate(siteUrl, UriKind.Absolute, out var uri) ? uri.AbsolutePath.TrimEnd('/') : string.Empty;

    /// <summary>
    /// Nettoie un chemin construit depuis le gabarit. Un jeton non renseigné —
    /// une certification encore inconnue, par exemple — laisserait sinon des
    /// séparateurs orphelins du genre « Camille DUPONT -  » dans le nom du dossier.
    /// </summary>
    public static string NettoyerChemin(string chemin)
    {
        var segments = chemin
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(NettoyerSegment)
            .Where(s => s.Length > 0);

        return string.Join('/', segments);
    }

    private static string NettoyerSegment(string segment)
    {
        var s = segment.Trim();

        // Retire les séparateurs de fin ou de début laissés par un jeton vide.
        s = s.Trim(' ', '-', '_', '·');

        while (s.Contains("  ", StringComparison.Ordinal))
        {
            s = s.Replace("  ", " ", StringComparison.Ordinal);
        }

        // « Nom -  - Prénom » devient « Nom - Prénom ».
        while (s.Contains("- -", StringComparison.Ordinal))
        {
            s = s.Replace("- -", "-", StringComparison.Ordinal);
        }

        return s.Trim().TrimEnd('.');
    }

    /// <summary>
    /// Normalise un fragment de nom de dossier. SharePoint refuse
    /// <c>" * : &lt; &gt; ? / \ |</c> et les points en fin de segment.
    /// </summary>
    public static string Nettoyer(string? valeur)
    {
        if (string.IsNullOrWhiteSpace(valeur))
        {
            return string.Empty;
        }

        var sb = new StringBuilder(valeur.Length);
        foreach (var c in valeur.Trim())
        {
            sb.Append(c switch
            {
                '"' or '*' or ':' or '<' or '>' or '?' or '/' or '\\' or '|' => '-',
                '\n' or '\r' or '\t' => ' ',
                _ => c,
            });
        }

        var sortie = sb.ToString().Trim().TrimEnd('.');
        while (sortie.Contains("  ", StringComparison.Ordinal))
        {
            sortie = sortie.Replace("  ", " ", StringComparison.Ordinal);
        }

        return sortie;
    }
}
