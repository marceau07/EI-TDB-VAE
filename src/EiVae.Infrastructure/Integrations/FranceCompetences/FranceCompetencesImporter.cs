using System.Globalization;
using System.IO.Compression;
using System.Text.Json;
using System.Xml;
using EiVae.Domain;
using EiVae.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EiVae.Infrastructure.Integrations.FranceCompetences;

public sealed class FranceCompetencesOptions
{
    public const string Section = "FranceCompetences";

    /// <summary>Jeu de données data.gouv.fr publiant les exports RNCP et RS.</summary>
    public string DatasetUrl { get; set; } =
        "https://www.data.gouv.fr/api/1/datasets/repertoire-national-des-certifications-professionnelles-et-repertoire-specifique/";

    /// <summary>Préfixe du fichier recherché parmi les ressources du jeu de données.</summary>
    public string PrefixeExport { get; set; } = "export-fiches-rncp-v4-1";

    /// <summary>
    /// Limiter l'import aux certifications déjà présentes au catalogue. Le
    /// répertoire compte plus de 25 000 fiches : n'en stocker que le périmètre
    /// utile garde la base lisible et les recherches rapides.
    /// </summary>
    public bool LimiterAuCatalogue { get; set; } = true;

    public bool ActiverSynchronisationAutomatique { get; set; } = true;

    /// <summary>Heure d'exécution quotidienne, en heure locale du serveur.</summary>
    public TimeOnly HeureSynchronisation { get; set; } = new(4, 30);
}

/// <summary>
/// Import du Répertoire national des certifications professionnelles.
///
/// Source : export officiel publié quotidiennement par France Compétences sur
/// data.gouv.fr, en accès libre et sans authentification. Le fichier XML pèse
/// environ 470 Mo décompressé : il est lu en flux, jamais chargé en mémoire.
///
/// L'import ne touche que les champs dont France Compétences est la source de
/// vérité. Les champs internes — abrégé, domaine EI, statut interne, durée
/// habituelle, particularités, contacts, habilitations, modules — ne sont jamais
/// écrasés : c'est cette séparation qui rend la synchronisation quotidienne sûre.
/// </summary>
public sealed class FranceCompetencesImporter(
    VaeDbContext db,
    IHttpClientFactory httpClientFactory,
    IOptions<FranceCompetencesOptions> options,
    ILogger<FranceCompetencesImporter> logger)
{
    private readonly FranceCompetencesOptions _options = options.Value;

    /// <summary>Champs pilotés par France Compétences, listés pour la documentation d'exploitation.</summary>
    public static IReadOnlyList<string> ChampsSynchronises { get; } =
    [
        "Intitulé", "État de la fiche", "Niveau", "Type d'enregistrement", "Dates de décision et de fin",
        "Voies d'accès dont VAE", "Composition du jury VAE", "Blocs de compétences", "Certificateurs",
        "Codes NSF, Formacode et ROME", "Activités visées", "Capacités attestées", "Prérequis", "Statistiques",
    ];

    public static IReadOnlyList<string> ChampsInternes { get; } =
    [
        "Abrégé", "Domaine EI Groupe", "Statut interne", "Durée habituelle", "Particularités",
        "Contact certificateur", "AAP et accompagnateurs habilités", "Modules EI Académie",
    ];

    /// <summary>Localise l'export le plus récent publié sur data.gouv.fr.</summary>
    public async Task<(string Url, string Titre)?> TrouverDernierExportAsync(CancellationToken ct = default)
    {
        var http = httpClientFactory.CreateClient(nameof(FranceCompetencesImporter));
        using var reponse = await http.GetAsync(_options.DatasetUrl, ct).ConfigureAwait(false);
        reponse.EnsureSuccessStatusCode();

        await using var flux = await reponse.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var doc = await JsonDocument.ParseAsync(flux, cancellationToken: ct).ConfigureAwait(false);

        if (!doc.RootElement.TryGetProperty("resources", out var ressources))
        {
            return null;
        }

        (string Url, string Titre, DateTimeOffset Date)? meilleure = null;

        foreach (var r in ressources.EnumerateArray())
        {
            var titre = r.TryGetProperty("title", out var t) ? t.GetString() ?? string.Empty : string.Empty;
            if (!titre.StartsWith(_options.PrefixeExport, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var url = r.TryGetProperty("url", out var u) ? u.GetString() : null;
            if (string.IsNullOrWhiteSpace(url))
            {
                continue;
            }

            var date = r.TryGetProperty("last_modified", out var lm)
                       && DateTimeOffset.TryParse(lm.GetString(), CultureInfo.InvariantCulture,
                           DateTimeStyles.RoundtripKind, out var d)
                ? d
                : DateTimeOffset.MinValue;

            if (meilleure is null || date > meilleure.Value.Date)
            {
                meilleure = (url, titre, date);
            }
        }

        return meilleure is null ? null : (meilleure.Value.Url, meilleure.Value.Titre);
    }

    /// <summary>Télécharge et importe le dernier export publié.</summary>
    public async Task<ImportRun> SynchroniserAsync(string? declencheur = null, CancellationToken ct = default)
    {
        var run = new ImportRun
        {
            Source = SourceImport.FranceCompetences,
            Declencheur = declencheur ?? "automatique",
        };
        db.Imports.Add(run);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        var journal = new List<string>();

        try
        {
            var export = await TrouverDernierExportAsync(ct).ConfigureAwait(false)
                ?? throw new InvalidOperationException(
                    "Aucun export RNCP n'a été trouvé dans le jeu de données data.gouv.fr.");

            run.Reference = export.Titre;
            journal.Add($"Export retenu : {export.Titre}");
            logger.LogInformation("France Compétences — téléchargement de {Titre}", export.Titre);

            var fichier = Path.Combine(Path.GetTempPath(), $"rncp-{Guid.NewGuid():N}.zip");
            try
            {
                var http = httpClientFactory.CreateClient(nameof(FranceCompetencesImporter));
                await using (var source = await http.GetStreamAsync(export.Url, ct).ConfigureAwait(false))
                await using (var cible = File.Create(fichier))
                {
                    await source.CopyToAsync(cible, ct).ConfigureAwait(false);
                }

                journal.Add($"Archive téléchargée : {new FileInfo(fichier).Length / 1_048_576} Mo");
                await ImporterArchiveAsync(fichier, run, journal, ct).ConfigureAwait(false);
            }
            finally
            {
                TryDelete(fichier);
            }

            run.Statut = run.NombreErreurs > 0 ? StatutImport.TermineAvecErreurs : StatutImport.Termine;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            run.Statut = StatutImport.Echec;
            run.NombreErreurs++;
            journal.Add($"Échec : {ex.Message}");
            logger.LogError(ex, "France Compétences — l'import a échoué");
        }

        run.TermineLe = DateTimeOffset.UtcNow;
        run.Journal = string.Join('\n', journal);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return run;
    }

    /// <summary>Importe une archive déjà présente sur le disque. Utilisé aussi par les tests.</summary>
    public async Task ImporterArchiveAsync(
        string cheminZip, ImportRun run, List<string> journal, CancellationToken ct = default)
    {
        var codesAttendus = _options.LimiterAuCatalogue
            ? await db.Certifications.Select(c => c.CodeRncp).ToHashSetAsync(StringComparer.OrdinalIgnoreCase, ct)
                .ConfigureAwait(false)
            : null;

        if (codesAttendus is { Count: 0 })
        {
            journal.Add("Catalogue vide : import de l'intégralité des fiches publiées.");
            codesAttendus = null;
        }

        using var archive = ZipFile.OpenRead(cheminZip);
        var entree = archive.Entries.FirstOrDefault(e => e.Name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("L'archive ne contient aucun fichier XML.");

        await using var flux = entree.Open();
        using var lecteur = XmlReader.Create(flux, new XmlReaderSettings
        {
            IgnoreWhitespace = true,
            IgnoreComments = true,
            DtdProcessing = DtdProcessing.Prohibit,
            Async = false,
        });

        var certificateurs = await db.Certificateurs.ToDictionaryAsync(
            c => c.Siret ?? c.Nom, StringComparer.OrdinalIgnoreCase, ct).ConfigureAwait(false);

        var lot = 0;

        while (lecteur.Read())
        {
            ct.ThrowIfCancellationRequested();

            if (lecteur.NodeType != XmlNodeType.Element || lecteur.Name != "FICHE")
            {
                continue;
            }

            run.NombreLus++;
            var fiche = FicheRncp.Lire(lecteur);
            if (fiche is null)
            {
                continue;
            }

            if (codesAttendus is not null && !codesAttendus.Contains(fiche.CodeRncp))
            {
                run.NombreIgnores++;
                continue;
            }

            try
            {
                await AppliquerAsync(fiche, certificateurs, run, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                run.NombreErreurs++;
                journal.Add($"{fiche.CodeRncp} : {ex.Message}");
                logger.LogWarning(ex, "France Compétences — fiche {Code} non importée", fiche.CodeRncp);
            }

            if (++lot >= 50)
            {
                await db.SaveChangesAsync(ct).ConfigureAwait(false);
                lot = 0;
            }
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        journal.Add(
            $"{run.NombreLus} fiches parcourues, {run.NombreCrees} créées, {run.NombreMisAJour} mises à jour, {run.NombreIgnores} hors périmètre.");
    }

    private async Task AppliquerAsync(
        FicheRncp fiche,
        Dictionary<string, Certificateur> certificateurs,
        ImportRun run,
        CancellationToken ct)
    {
        var certification = await db.Certifications
            .Include(c => c.Blocs)
            .Include(c => c.Certificateurs)
            .FirstOrDefaultAsync(c => c.CodeRncp == fiche.CodeRncp, ct)
            .ConfigureAwait(false);

        if (certification is null)
        {
            certification = new Certification
            {
                CodeRncp = fiche.CodeRncp,
                Intitule = fiche.Intitule,
                DomaineEi = fiche.DomaineDeduit,
                StatutInterne = StatutCertificationInterne.NonCouverte,
            };
            db.Certifications.Add(certification);
            run.NombreCrees++;
        }
        else
        {
            run.NombreMisAJour++;
        }

        // ---- champs pilotés par France Compétences ----
        certification.IdFiche = fiche.IdFiche;
        certification.Intitule = fiche.Intitule;
        certification.EtatFiche = fiche.EtatFiche;
        certification.ActifFranceCompetences = fiche.Actif;
        certification.Niveau = fiche.Niveau;
        certification.LibelleNiveau = fiche.LibelleNiveau;
        certification.TypeEnregistrement = fiche.TypeEnregistrement;
        certification.DateDecision = fiche.DateDecision;
        certification.DateFinEnregistrement = fiche.DateFinEnregistrement;
        certification.DateLimiteDelivrance = fiche.DateLimiteDelivrance;
        certification.DateDerniereModificationFiche = fiche.DateDerniereModification;
        certification.VoieVaeOuverte = fiche.VoieVae;
        certification.CompositionJuryVae = fiche.CompositionJuryVae;
        certification.VoieFormationInitiale = fiche.VoieFormationInitiale;
        certification.VoieFormationContinue = fiche.VoieFormationContinue;
        certification.VoieApprentissage = fiche.VoieApprentissage;
        certification.VoieContratProfessionnalisation = fiche.VoieContratProfessionnalisation;
        certification.VoieCandidatLibre = fiche.VoieCandidatLibre;
        certification.ActivitesVisees = fiche.ActivitesVisees;
        certification.CapacitesAttestees = fiche.CapacitesAttestees;
        certification.SecteursActivite = fiche.SecteursActivite;
        certification.TypeEmploiAccessibles = fiche.TypeEmploiAccessibles;
        certification.ObjectifsContexte = fiche.ObjectifsContexte;
        certification.Prerequis = fiche.Prerequis;
        certification.ReglementationActivites = fiche.ReglementationActivites;
        certification.CodesNsfJson = fiche.CodesNsfJson;
        certification.FormacodesJson = fiche.FormacodesJson;
        certification.CodesRomeJson = fiche.CodesRomeJson;
        certification.StatistiquesJson = fiche.StatistiquesJson;
        certification.DerniereSynchronisation = DateTimeOffset.UtcNow;

        // Le domaine EI n'est déduit qu'à la création : une reclassification
        // manuelle par le service doit survivre aux synchronisations suivantes.
        certification.DomaineEi ??= fiche.DomaineDeduit;

        SynchroniserBlocs(certification, fiche);
        await SynchroniserCertificateursAsync(certification, fiche, certificateurs, ct).ConfigureAwait(false);
    }

    private void SynchroniserBlocs(Certification certification, FicheRncp fiche)
    {
        var existants = certification.Blocs.ToDictionary(b => b.Code, StringComparer.OrdinalIgnoreCase);
        var vus = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var ordre = 0;
        foreach (var b in fiche.Blocs)
        {
            ordre++;
            vus.Add(b.Code);

            if (existants.TryGetValue(b.Code, out var bloc))
            {
                bloc.Libelle = b.Libelle;
                bloc.Competences = b.Competences;
                bloc.ModalitesEvaluation = b.ModalitesEvaluation;
                bloc.Ordre = ordre;
            }
            else
            {
                certification.Blocs.Add(new BlocCompetences
                {
                    Code = b.Code,
                    Libelle = b.Libelle,
                    Competences = b.Competences,
                    ModalitesEvaluation = b.ModalitesEvaluation,
                    Ordre = ordre,
                });
            }
        }

        // Un bloc retiré du référentiel doit disparaître : le conserver ferait
        // apparaître des blocs non validables dans les parcours sur blocs.
        foreach (var obsolete in existants.Where(kv => !vus.Contains(kv.Key)).Select(kv => kv.Value).ToList())
        {
            certification.Blocs.Remove(obsolete);
            db.Blocs.Remove(obsolete);
        }
    }

    private async Task SynchroniserCertificateursAsync(
        Certification certification,
        FicheRncp fiche,
        Dictionary<string, Certificateur> cache,
        CancellationToken ct)
    {
        var premier = true;

        foreach (var c in fiche.Certificateurs)
        {
            // Un SIRET vide doit rester NULL : l'index unique rejetterait
            // la deuxieme chaine vide inseree.
            var siret = string.IsNullOrWhiteSpace(c.Siret) ? null : c.Siret.Trim();
            var cle = siret ?? c.Nom;

            if (string.IsNullOrWhiteSpace(cle))
            {
                continue;
            }

            if (!cache.TryGetValue(cle, out var certificateur))
            {
                certificateur = await db.Certificateurs.FirstOrDefaultAsync(
                    x => (siret != null && x.Siret == siret) || (siret == null && x.Nom == c.Nom), ct)
                    .ConfigureAwait(false);

                if (certificateur is null)
                {
                    certificateur = new Certificateur { Nom = c.Nom, Siret = siret, Etat = c.Etat };
                    db.Certificateurs.Add(certificateur);
                }

                cache[cle] = certificateur;
            }

            certificateur.Nom = c.Nom;
            certificateur.Etat = c.Etat;

            if (!certification.Certificateurs.Any(x => x.Certificateur == certificateur
                                                       || (certificateur.Id != 0 && x.CertificateurId == certificateur.Id)))
            {
                certification.Certificateurs.Add(new CertificationCertificateur
                {
                    Certification = certification,
                    Certificateur = certificateur,
                    EstPrincipal = premier,
                });
            }

            premier = false;
        }
    }

    private static void TryDelete(string chemin)
    {
        try
        {
            if (File.Exists(chemin))
            {
                File.Delete(chemin);
            }
        }
        catch (IOException)
        {
            // Fichier temporaire encore verrouillé : il sera purgé par le système.
        }
    }
}
