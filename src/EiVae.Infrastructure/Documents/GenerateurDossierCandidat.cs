using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using EiVae.Domain;
using EiVae.Domain.Entities;
using EiVae.Domain.Services;
using EiVae.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EiVae.Infrastructure.Documents;

/// <summary>Emplacement où l'application crée les dossiers candidats.</summary>
public sealed class DossiersCandidatsOptions
{
    public const string Section = "DossiersCandidats";

    /// <summary>
    /// Dossier local correspondant à la racine « Candidats » de SharePoint. Sur le
    /// serveur, c'est la bibliothèque synchronisée par OneDrive : ce qui est créé
    /// ici apparaît dans SharePoint. Vide : aucun dossier n'est créé, la fiche Word
    /// reste téléchargeable depuis l'application.
    /// </summary>
    public string Racine { get; set; } = string.Empty;

    public bool EstConfigure => !string.IsNullOrWhiteSpace(Racine);
}

public sealed record ResultatDossier(
    string Chemin, string? CheminLocal, string NomFichier, bool Ecrit, string? Avertissement);

/// <summary>
/// Crée le dossier « NOM_Prenom_Certification » d'un candidat et y dépose une
/// fiche Word reprenant ses informations. Le nom suit le gabarit SharePoint, pour
/// que le lien profond de la fiche ouvre exactement ce dossier.
/// </summary>
public sealed class GenerateurDossierCandidat(
    VaeDbContext db,
    ParametresService parametres,
    SharePointLinkBuilder sharePoint,
    IOptions<SharePointOptions> sharePointOptions,
    IOptions<DossiersCandidatsOptions> options,
    ILogger<GenerateurDossierCandidat> logger)
{
    private static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");
    private const string Teal = "0E6D7D";
    private const string Gris = "5F787F";

    public bool EstConfigure => options.Value.EstConfigure;

    /// <summary>Crée le dossier s'il n'existe pas et (ré)écrit la fiche Word.</summary>
    public async Task<ResultatDossier> GenererAsync(int parcoursId, CancellationToken ct = default)
    {
        var p = await ChargerAsync(parcoursId, ct).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"Dossier {parcoursId} introuvable.");

        // Le chemin est figé à la première génération : renommer le candidat ou
        // changer de certification ne doit pas « perdre » le dossier existant.
        var chemin = sharePoint.CheminDossierCandidat(p);
        if (string.IsNullOrWhiteSpace(p.SharePointDossier))
        {
            p.SharePointDossier = chemin;
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }

        var nomFichier = NomFichier(p);
        if (!options.Value.EstConfigure)
        {
            return new ResultatDossier(chemin, null, nomFichier, false,
                "Aucun emplacement de dossiers n'est configuré (DossiersCandidats:Racine) : "
                + "la fiche Word reste téléchargeable depuis la fiche du candidat.");
        }

        var dossier = Path.Combine(options.Value.Racine, CheminRelatifLocal(chemin));
        var fichier = Path.Combine(dossier, nomFichier);

        try
        {
            Directory.CreateDirectory(dossier);
            var octets = await FicheWordAsync(p, ct).ConfigureAwait(false);
            await File.WriteAllBytesAsync(fichier, octets, ct).ConfigureAwait(false);
            logger.LogInformation("Dossier candidat {Parcours} généré : {Chemin}", p.Id, fichier);
            return new ResultatDossier(chemin, dossier, nomFichier, true, null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Dossier candidat {Parcours} : écriture impossible dans {Chemin}", p.Id, dossier);
            return new ResultatDossier(chemin, dossier, nomFichier, false,
                $"Écriture impossible dans {dossier} : {ex.Message}");
        }
    }

    /// <summary>
    /// Régénère la fiche après une modification du dossier. Sans emplacement
    /// configuré, rien à faire : le téléchargement produit toujours la version
    /// à jour. Un échec d'écriture — fichier ouvert dans Word, par exemple —
    /// n'empêche jamais l'enregistrement du dossier.
    /// </summary>
    public async Task<ResultatDossier?> MettreAJourAsync(int parcoursId, CancellationToken ct = default)
    {
        if (!options.Value.EstConfigure)
        {
            return null;
        }

        try
        {
            return await GenererAsync(parcoursId, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Fiche Word du dossier {Parcours} non régénérée", parcoursId);
            return null;
        }
    }

    /// <summary>
    /// Lien vers la fiche Word : le fichier dans SharePoint quand le dossier y
    /// est synchronisé, sinon le téléchargement depuis l'application.
    /// </summary>
    public (string Url, string Libelle) LienFiche(Parcours p)
    {
        if (options.Value.EstConfigure && sharePoint.EstConfigure
            && sharePoint.UrlDepuisChemin($"{sharePoint.CheminDossierCandidat(p)}/{NomFichier(p)}") is { } url)
        {
            return (url, "Ouvrir la fiche Word dans SharePoint");
        }

        return ($"/api/parcours/{p.Id}/fiche.docx", "Télécharger la fiche Word");
    }

    /// <summary>Fiche Word seule, pour le téléchargement.</summary>
    public async Task<(byte[] Contenu, string NomFichier)?> FicheAsync(int parcoursId, CancellationToken ct = default)
    {
        var p = await ChargerAsync(parcoursId, ct).ConfigureAwait(false);
        return p is null ? null : (await FicheWordAsync(p, ct).ConfigureAwait(false), NomFichier(p));
    }

    private Task<Parcours?> ChargerAsync(int parcoursId, CancellationToken ct) =>
        db.Parcours
            .Include(x => x.Candidat)
            .Include(x => x.Certification).ThenInclude(c => c!.Certificateurs).ThenInclude(cc => cc.Certificateur)
            .Include(x => x.Aap)
            .Include(x => x.Accompagnateur)
            .Include(x => x.Financements)
            .Include(x => x.Factures)
            .Include(x => x.Modules).ThenInclude(m => m.ModuleAcademie)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == parcoursId, ct);

    /// <summary>
    /// Le chemin SharePoint commence par la racine des candidats, qui correspond
    /// au dossier local configuré : on ne garde que ce qui vient après.
    /// </summary>
    private string CheminRelatifLocal(string chemin)
    {
        var racine = sharePointOptions.Value.RacineCandidats.Trim('/');
        var relatif = chemin.StartsWith(racine + "/", StringComparison.OrdinalIgnoreCase)
            ? chemin[(racine.Length + 1)..]
            : chemin;

        return relatif.Replace('/', Path.DirectorySeparatorChar);
    }

    public static string NomFichier(Parcours p) =>
        $"Fiche_candidat_{SharePointLinkBuilder.Nettoyer(p.Candidat?.Nom)}_{SharePointLinkBuilder.Nettoyer(p.Candidat?.Prenom)}.docx";

    private async Task<byte[]> FicheWordAsync(Parcours p, CancellationToken ct)
    {
        var tarification = await parametres.TarificationAsync(ct).ConfigureAwait(false);
        var calcul = tarification.Calculer(p);
        var grille = tarification.GrillePour(p);
        var c = p.Candidat!;
        var cert = p.Certification;

        using var flux = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(flux, WordprocessingDocumentType.Document))
        {
            var main = doc.AddMainDocumentPart();
            var corps = new Body();
            main.Document = new Document(corps);

            corps.Append(Texte("EI Groupe — Service VAE", 9, Gris, espaceApres: 60));
            corps.Append(Texte("Fiche candidat", 22, Teal, gras: true, espaceApres: 40));
            corps.Append(Texte($"{c.Prenom} {c.Nom}", 14, null, gras: true, espaceApres: 240));

            Section(corps, "Identité", [
                ("Nom", c.Nom),
                ("Prénom", c.Prenom),
                ("Date de naissance", Date(c.DateNaissance)),
                ("Courriel", c.Email),
                ("Téléphone", c.Telephone),
                ("Ville", Joindre(c.CodePostal, c.Ville)),
                ("Département", c.Departement),
                ("Région", c.Region),
            ]);

            Section(corps, "Demande", [
                ("Certification visée", cert is null ? "à déterminer"
                    : (cert.Abrege is { Length: > 0 } a ? $"{a} — " : string.Empty) + cert.Intitule),
                ("Code RNCP", cert?.CodeRncp),
                ("Niveau", cert?.Niveau?.ToString(Fr)),
                ("Certificateur", cert?.Certificateurs.OrderByDescending(x => x.EstPrincipal)
                    .Select(x => x.Certificateur?.Nom).FirstOrDefault()),
                ("Origine", ServiceNotifications.LibelleOrigine(p.Origine)),
                ("Date de première demande", Date(p.DateDemande)),
                ("Étape", MoteurAlertes.Libelle(p.Etape)),
                ("Identifiant France VAE", p.CandidatureFranceVaeId),
            ]);

            Section(corps, "Acteurs", [
                ("AAP référent", p.Aap?.NomComplet ?? "non affecté"),
                ("Accompagnateur", p.Accompagnateur?.NomComplet ?? "non affecté"),
            ]);

            Section(corps, "Parcours prescrit", [
                ("Date de démarrage", Date(p.DateDebutParcours)),
                ("Grille tarifaire", $"{grille.Code} — {grille.Libelle}"),
                ("Heures individuelles", Heures(p.HeuresIndividuelPrescrites)),
                ("Heures collectives", Heures(p.HeuresCollectifPrescrites)),
                ("Compléments formatifs", Heures(p.HeuresComplementFormatifPrescrites)),
                ("Montant prescrit", calcul.Total.ToString("N0", Fr) + " € HT"),
                ("Code ACF Solei", p.CodeAcfSolei),
            ]);

            Liste(corps, "Compléments formatifs & e-learning",
                ["Nature", "Module", "Durée", "Statut", "Attribué le"],
                [1900, 3900, 1000, 1500, 1338],
                p.Modules.OrderBy(m => m.ModuleAcademie!.Nature).ThenBy(m => m.ModuleAcademie!.Titre)
                    .Select(m => new[]
                    {
                        m.ModuleAcademie!.Nature == NatureModule.ComplementFormatif ? "Complément formatif" : "E-learning",
                        m.ModuleAcademie.Titre,
                        m.ModuleAcademie.DureeHeures is { } h ? Heures(h) : null,
                        LibelleStatutModule(m.Statut),
                        Date(m.DateAttribution),
                    }).ToList(),
                "Aucun complément formatif ni module e-learning prescrit.");

            Section(corps, "Jalons & jury", [
                ("Demande reçue", Date(p.DateDemande)),
                ("Premier contact", Date(p.DatePremierContact)),
                ("Recueil des besoins", Date(p.DateRecueilBesoins)),
                ("RDV faisabilité", Date(p.DateRdvFaisabilite)),
                ("Dépôt faisabilité", Date(p.DateDepotFaisabilite)),
                ("Recevabilité", Date(p.DateRecevabilite)),
                ("Parcours validé", Date(p.DateParcoursValide)),
                ("Début accompagnement", Date(p.DateDebutAccompagnement)),
                ("Dépôt dossier de validation", Date(p.DateDepotDossierValidation)),
                ("Passage en jury", Date(p.DateJury)),
                ("Entretien post-jury", Date(p.DateEntretienPostJury)),
                ("Résultat de jury", LibelleResultat(p.ResultatJury)),
                ("Commentaire du jury", p.CommentaireJury),
                ("Motif de sortie", p.MotifSortie == MotifSortie.Aucun ? null : LibelleSortie(p.MotifSortie)),
            ]);

            Liste(corps, "Financement",
                ["Dispositif", "Financeur", "N° de prise en charge", "Accordé", "Reste à charge", "Sécurisé le"],
                [1500, 1900, 1900, 1400, 1400, 1538],
                p.Financements.OrderBy(f => f.DateDemande).Select(f => new[]
                {
                    LibelleDispositif(f.Dispositif), f.Financeur, f.NumeroPriseEnCharge,
                    Montant(f.MontantAccorde), Montant(f.ResteACharge),
                    f.DateSecurisation is { } d ? Date(d) : "non sécurisé",
                }).ToList(),
                "Aucun financement enregistré.");

            Liste(corps, "Factures",
                ["Numéro", "Émise le", "Financeur", "Montant HT", "Réglée le"],
                [1800, 1500, 2600, 1800, 1938],
                p.Factures.OrderBy(f => f.DateEmission).Select(f => new[]
                {
                    f.Numero, Date(f.DateEmission), f.Financeur, Montant(f.MontantHt),
                    f.DateReglement is { } d ? Date(d) : "en attente",
                }).ToList(),
                "Aucune facture émise.",
                p.Factures.Count > 0
                    ? $"Total facturé : {Montant(p.Factures.Sum(f => f.MontantHt))} HT — "
                      + $"réglé : {Montant(p.Factures.Where(f => f.EstReglee).Sum(f => f.MontantHt))} HT"
                    : null);

            Section(corps, "Conformité", [
                ("Consentement RGPD", c.ConsentementRgpd
                    ? "recueilli" + (c.DateConsentementRgpd is { } d ? $" le {d.ToString("dd/MM/yyyy", Fr)}" : string.Empty)
                    : "non recueilli"),
            ]);

            if (!string.IsNullOrWhiteSpace(c.Notes))
            {
                corps.Append(Texte("Notes", 12, Teal, gras: true, espaceAvant: 240, espaceApres: 80));
                foreach (var ligne in c.Notes.Split('\n'))
                {
                    corps.Append(Texte(ligne.TrimEnd('\r'), 10, null));
                }
            }

            corps.Append(Texte(
                $"Document généré le {DateTime.Now.ToString("dd/MM/yyyy 'à' HH:mm", Fr)} — dossier n° {p.Id}.",
                8, Gris, espaceAvant: 360));

            corps.Append(new SectionProperties(
                new PageSize { Width = 11906, Height = 16838 },
                new PageMargin { Top = 1134, Bottom = 1134, Left = 1134, Right = 1134, Header = 567, Footer = 567 }));

            main.Document.Save();
        }

        return flux.ToArray();
    }

    private static void Section(Body corps, string titre, (string Libelle, string? Valeur)[] lignes)
    {
        corps.Append(Texte(titre, 12, Teal, gras: true, espaceAvant: 200, espaceApres: 60));

        var bordure = new TableBorders(
            new TopBorder { Val = BorderValues.Single, Size = 4, Color = "D5E3E6" },
            new BottomBorder { Val = BorderValues.Single, Size = 4, Color = "D5E3E6" },
            new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4, Color = "D5E3E6" });

        var table = new Table(new TableProperties(
            bordure,
            new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct },
            new TableLayout { Type = TableLayoutValues.Fixed }));

        table.Append(new TableGrid(new GridColumn { Width = "3200" }, new GridColumn { Width = "6438" }));

        foreach (var (libelle, valeur) in lignes)
        {
            table.Append(new TableRow(
                Cellule(libelle, 3200, Gris),
                Cellule(string.IsNullOrWhiteSpace(valeur) ? "—" : valeur, 6438, null)));
        }

        corps.Append(table);
    }

    /// <summary>Tableau à plusieurs colonnes, pour les financements et les factures.</summary>
    private static void Liste(
        Body corps, string titre, string[] entetes, int[] largeurs, List<string?[]> lignes,
        string siVide, string? pied = null)
    {
        corps.Append(Texte(titre, 12, Teal, gras: true, espaceAvant: 200, espaceApres: 60));

        if (lignes.Count == 0)
        {
            corps.Append(Texte(siVide, 10, Gris));
            return;
        }

        var table = new Table(new TableProperties(
            new TableBorders(
                new TopBorder { Val = BorderValues.Single, Size = 4, Color = "D5E3E6" },
                new BottomBorder { Val = BorderValues.Single, Size = 4, Color = "D5E3E6" },
                new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4, Color = "D5E3E6" }),
            new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct },
            new TableLayout { Type = TableLayoutValues.Fixed }));

        table.Append(new TableGrid(largeurs.Select(l =>
            new GridColumn { Width = l.ToString(CultureInfo.InvariantCulture) })));
        table.Append(new TableRow(entetes.Select((e, i) => Cellule(e, largeurs[i], Gris))));

        foreach (var ligne in lignes)
        {
            table.Append(new TableRow(ligne.Select((v, i) =>
                Cellule(string.IsNullOrWhiteSpace(v) ? "—" : v, largeurs[i], null))));
        }

        corps.Append(table);
        if (pied is not null)
        {
            corps.Append(Texte(pied, 9, Gris, espaceAvant: 60));
        }
    }

    private static TableCell Cellule(string texte, int largeur, string? couleur) =>
        new(
            new TableCellProperties(
                new TableCellWidth { Width = largeur.ToString(CultureInfo.InvariantCulture), Type = TableWidthUnitValues.Dxa },
                new TableCellMargin(
                    new TopMargin { Width = "30", Type = TableWidthUnitValues.Dxa },
                    new BottomMargin { Width = "30", Type = TableWidthUnitValues.Dxa })),
            Texte(texte, 10, couleur));

    private static Paragraph Texte(
        string texte, int taillePoints, string? couleur, bool gras = false,
        int espaceAvant = 0, int espaceApres = 0)
    {
        var proprietes = new RunProperties(
            new RunFonts { Ascii = "Calibri", HighAnsi = "Calibri", ComplexScript = "Calibri" },
            new FontSize { Val = (taillePoints * 2).ToString(CultureInfo.InvariantCulture) });

        if (gras)
        {
            proprietes.Append(new Bold());
        }

        if (couleur is not null)
        {
            proprietes.Append(new Color { Val = couleur });
        }

        return new Paragraph(
            new ParagraphProperties(new SpacingBetweenLines
            {
                Before = espaceAvant.ToString(CultureInfo.InvariantCulture),
                After = espaceApres.ToString(CultureInfo.InvariantCulture),
            }),
            new Run(proprietes, new Text(texte) { Space = SpaceProcessingModeValues.Preserve }));
    }

    private static string? Date(DateOnly? d) => d?.ToString("dd/MM/yyyy", Fr);

    private static string? Montant(decimal? m) =>
        m is { } v ? v.ToString("N2", Fr).Replace('\u202f', ' ') + " €" : null;

    private static string LibelleStatutModule(StatutModule s) => s switch
    {
        StatutModule.Prescrit => "prescrit",
        StatutModule.EnCours => "en cours",
        StatutModule.Termine => "terminé",
        StatutModule.Abandonne => "abandonné",
        _ => s.ToString(),
    };

    private static string LibelleResultat(ResultatJury r) => r switch
    {
        ResultatJury.ValidationTotale => "Validation totale",
        ResultatJury.ValidationPartielle => "Validation partielle",
        ResultatJury.Refus => "Refus",
        ResultatJury.Absence => "Absence",
        _ => "non renseigné",
    };

    private static string LibelleSortie(MotifSortie m) => m switch
    {
        MotifSortie.AbandonCandidat => "Abandon candidat",
        MotifSortie.AbandonAap => "Abandon AAP",
        MotifSortie.DisparuFranceVae => "Disparu de France VAE",
        MotifSortie.CandidatureSupprimee => "Candidature supprimée",
        MotifSortie.Reorientation => "Réorientation",
        MotifSortie.FinancementNonObtenu => "Financement non obtenu",
        _ => m.ToString(),
    };

    private static string LibelleDispositif(DispositifFinancement d) => d switch
    {
        DispositifFinancement.Cpf => "CPF",
        DispositifFinancement.Opco => "OPCO",
        DispositifFinancement.FranceTravail => "France Travail",
        DispositifFinancement.TransitionPro => "Transition Pro",
        DispositifFinancement.Region => "Région",
        DispositifFinancement.NonSecurise => "Non sécurisé",
        _ => d.ToString(),
    };

    private static string Heures(decimal h) => h == 0 ? "—" : h.ToString("0.#", Fr) + " h";

    private static string? Joindre(params string?[] parties)
    {
        var s = string.Join(" ", parties.Where(x => !string.IsNullOrWhiteSpace(x)));
        return s.Length == 0 ? null : s;
    }
}
