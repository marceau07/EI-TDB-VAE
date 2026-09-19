using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using EiVae.Domain;
using EiVae.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EiVae.Infrastructure.Integrations.FranceVae;

/// <summary>Correspondance entre les colonnes du fichier et les champs attendus.</summary>
public sealed class MappageColonnes
{
    public string? Nom { get; set; }
    public string? Prenom { get; set; }
    public string? Email { get; set; }
    public string? Telephone { get; set; }
    public string? Certification { get; set; }
    public string? CodeRncp { get; set; }
    public string? DateDemande { get; set; }
    public string? Departement { get; set; }
    public string? Ville { get; set; }
    public string? Statut { get; set; }
    public string? IdentifiantFranceVae { get; set; }

    /// <summary>
    /// Devine le mappage à partir des en-têtes du fichier. France VAE a fait
    /// évoluer les intitulés de son export : on reconnaît plusieurs variantes
    /// plutôt que d'imposer un gabarit figé.
    /// </summary>
    public static MappageColonnes Deviner(IEnumerable<string> entetes)
    {
        var m = new MappageColonnes();
        foreach (var e in entetes)
        {
            var n = Normaliser(e);
            if (m.Nom is null && n is "nom" or "nomcandidat" or "nomdenaissance" or "nomusage") m.Nom = e;
            else if (m.Prenom is null && n is "prenom" or "prenomcandidat") m.Prenom = e;
            else if (m.Email is null && (n.Contains("mail") || n.Contains("courriel"))) m.Email = e;
            else if (m.Telephone is null && (n.Contains("tel") || n.Contains("portable"))) m.Telephone = e;
            else if (m.CodeRncp is null && n.Contains("rncp")) m.CodeRncp = e;
            else if (m.Certification is null
                     && (n.Contains("certification") || n.Contains("diplome") || n.Contains("titre"))) m.Certification = e;
            else if (m.DateDemande is null
                     && (n.Contains("datecandidature") || n.Contains("datedemande") || n.Contains("datecreation"))) m.DateDemande = e;
            else if (m.Departement is null && n.Contains("departement")) m.Departement = e;
            else if (m.Ville is null && (n.Contains("ville") || n.Contains("commune"))) m.Ville = e;
            else if (m.Statut is null && (n.Contains("statut") || n.Contains("etat"))) m.Statut = e;
            else if (m.IdentifiantFranceVae is null
                     && (n is "id" or "idcandidature" or "identifiant" || n.Contains("uuid"))) m.IdentifiantFranceVae = e;
        }

        return m;
    }

    private static string Normaliser(string v)
    {
        var sb = new StringBuilder(v.Length);
        foreach (var c in v.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(c))
            {
                sb.Append(char.ToLowerInvariant(c));
            }
        }

        return sb.ToString();
    }
}

public sealed record ResultatImport(
    int Lus, int Crees, int MisAJour, int Ignores, int Erreurs, IReadOnlyList<string> Journal)
{
    /// <summary>Dossiers créés par l'import, pour les traitements qui suivent une création.</summary>
    public IReadOnlyList<int> ParcoursCrees { get; init; } = [];
}

/// <summary>Projection minimale du referentiel, suffisante pour le rapprochement.</summary>
internal sealed record CertificationRef(int Id, string CodeRncp, string Intitule, string? Abrege);

/// <summary>
/// Import des candidatures depuis un export du back-office France VAE.
///
/// C'est la voie d'alimentation réellement praticable aujourd'hui : l'API
/// d'interopérabilité n'ouvre pas de listing aux AAP. Le lecteur accepte les
/// séparateurs point-virgule et virgule, détecte l'encodage, et rapproche chaque
/// ligne d'un dossier existant par identifiant France VAE, puis par courriel,
/// puis par nom et prénom — dans cet ordre, du plus fiable au moins fiable.
/// </summary>
public sealed class ImportCandidaturesCsv(VaeDbContext db, ILogger<ImportCandidaturesCsv> logger)
{
    /// <summary>Lit les en-têtes sans importer : permet de proposer le mappage à l'utilisateur.</summary>
    public static IReadOnlyList<string> LireEntetes(Stream flux)
    {
        using var lecteur = new StreamReader(flux, Encoding.UTF8, detectEncodingFromByteOrderMarks: true,
            leaveOpen: true);
        var premiere = lecteur.ReadLine();
        if (string.IsNullOrWhiteSpace(premiere))
        {
            return [];
        }

        var separateur = Separateur(premiere);
        return premiere.Split(separateur).Select(c => c.Trim().Trim('"')).ToList();
    }

    public async Task<ResultatImport> ImporterAsync(
        Stream flux,
        MappageColonnes? mappage = null,
        bool simulation = false,
        string? declencheur = null,
        CancellationToken ct = default)
    {
        var journal = new List<string>();
        var nouveaux = new List<Parcours>();
        int lus = 0, crees = 0, majs = 0, ignores = 0, erreurs = 0;

        var run = new ImportRun
        {
            Source = SourceImport.FranceVaeExport,
            Declencheur = declencheur ?? "manuel",
            Reference = simulation ? "simulation" : null,
        };

        if (!simulation)
        {
            db.Imports.Add(run);
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }

        using var memoire = new MemoryStream();
        await flux.CopyToAsync(memoire, ct).ConfigureAwait(false);
        memoire.Position = 0;

        var entetes = LireEntetes(memoire);
        memoire.Position = 0;
        mappage ??= MappageColonnes.Deviner(entetes);

        if (mappage.Nom is null || mappage.Prenom is null)
        {
            journal.Add("Colonnes « nom » et « prénom » introuvables : import interrompu.");
            return Terminer(run, simulation, 0, 0, 0, 0, 1, journal, ct);
        }

        using var texte = new StreamReader(memoire, Encoding.UTF8, true);
        var premiere = await texte.ReadLineAsync(ct).ConfigureAwait(false) ?? string.Empty;
        memoire.Position = 0;

        var config = new CsvConfiguration(CultureInfo.GetCultureInfo("fr-FR"))
        {
            Delimiter = Separateur(premiere).ToString(),
            HasHeaderRecord = true,
            MissingFieldFound = null,
            HeaderValidated = null,
            BadDataFound = null,
            TrimOptions = TrimOptions.Trim,
        };

        using var lecteur = new StreamReader(memoire, Encoding.UTF8, true);
        using var csv = new CsvReader(lecteur, config);

        await csv.ReadAsync().ConfigureAwait(false);
        csv.ReadHeader();

        var certifications = await db.Certifications
            .Select(c => new CertificationRef(c.Id, c.CodeRncp, c.Intitule, c.Abrege))
            .ToListAsync(ct).ConfigureAwait(false);

        while (await csv.ReadAsync().ConfigureAwait(false))
        {
            ct.ThrowIfCancellationRequested();
            lus++;

            try
            {
                var nom = Champ(csv, mappage.Nom)?.Trim();
                var prenom = Champ(csv, mappage.Prenom)?.Trim();

                if (string.IsNullOrWhiteSpace(nom) || string.IsNullOrWhiteSpace(prenom))
                {
                    ignores++;
                    continue;
                }

                var idFvae = Champ(csv, mappage.IdentifiantFranceVae)?.Trim();
                var email = Champ(csv, mappage.Email)?.Trim();

                var parcours = await RechercherAsync(idFvae, email, nom, prenom, ct).ConfigureAwait(false);
                var nouveau = parcours is null;

                if (nouveau)
                {
                    var candidat = new Candidat
                    {
                        Nom = nom.ToUpperInvariant(),
                        Prenom = Capitaliser(prenom),
                        Email = email,
                        Telephone = Champ(csv, mappage.Telephone)?.Trim(),
                        Ville = Champ(csv, mappage.Ville)?.Trim(),
                        Departement = Champ(csv, mappage.Departement)?.Trim(),
                        IdentifiantFranceVae = string.IsNullOrWhiteSpace(idFvae) ? null : idFvae,
                    };

                    parcours = new Parcours
                    {
                        Candidat = candidat,
                        Origine = OrigineCandidature.FranceVae,
                        Etape = EtapeParcours.DemandeRecue,
                        CandidatureFranceVaeId = string.IsNullOrWhiteSpace(idFvae) ? null : idFvae,
                    };

                    if (!simulation)
                    {
                        db.Candidats.Add(candidat);
                        db.Parcours.Add(parcours);
                        nouveaux.Add(parcours);
                    }

                    crees++;
                }
                else
                {
                    majs++;
                }

                var dateDemande = Date(Champ(csv, mappage.DateDemande));
                if (dateDemande is not null)
                {
                    parcours!.DateDemande ??= dateDemande;
                }

                var codeRncp = Champ(csv, mappage.CodeRncp)?.Trim();
                var libelle = Champ(csv, mappage.Certification)?.Trim();
                var certification = Rapprocher(certifications, codeRncp, libelle);

                if (certification is not null && parcours!.CertificationId is null)
                {
                    parcours.CertificationId = certification.Id;
                }
                else if (certification is null && !string.IsNullOrWhiteSpace(libelle))
                {
                    journal.Add($"{nom} {prenom} : certification « {libelle} » non rapprochée du référentiel.");
                }

                parcours!.DateDernierMouvement = DateOnly.FromDateTime(DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                erreurs++;
                journal.Add($"Ligne {lus} : {ex.Message}");
                logger.LogWarning(ex, "Import CSV — ligne {Ligne} en erreur", lus);
            }
        }

        if (!simulation)
        {
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }

        journal.Insert(0, simulation
            ? $"Simulation : {crees} dossiers seraient créés, {majs} mis à jour."
            : $"{crees} dossiers créés, {majs} mis à jour.");

        return Terminer(run, simulation, lus, crees, majs, ignores, erreurs, journal, ct) with
        {
            ParcoursCrees = nouveaux.Select(p => p.Id).ToList(),
        };
    }

    private ResultatImport Terminer(
        ImportRun run, bool simulation, int lus, int crees, int majs, int ignores, int erreurs,
        List<string> journal, CancellationToken ct)
    {
        if (!simulation)
        {
            run.NombreLus = lus;
            run.NombreCrees = crees;
            run.NombreMisAJour = majs;
            run.NombreIgnores = ignores;
            run.NombreErreurs = erreurs;
            run.Statut = erreurs > 0 ? StatutImport.TermineAvecErreurs : StatutImport.Termine;
            run.TermineLe = DateTimeOffset.UtcNow;
            run.Journal = string.Join('\n', journal);
            db.SaveChanges();
        }

        _ = ct;
        return new ResultatImport(lus, crees, majs, ignores, erreurs, journal);
    }

    private async Task<Parcours?> RechercherAsync(
        string? idFvae, string? email, string nom, string prenom, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(idFvae))
        {
            var parId = await db.Parcours.Include(p => p.Candidat)
                .FirstOrDefaultAsync(p => p.CandidatureFranceVaeId == idFvae, ct).ConfigureAwait(false);
            if (parId is not null)
            {
                return parId;
            }
        }

        if (!string.IsNullOrWhiteSpace(email))
        {
            var parEmail = await db.Parcours.Include(p => p.Candidat)
                .FirstOrDefaultAsync(p => p.Candidat!.Email == email, ct).ConfigureAwait(false);
            if (parEmail is not null)
            {
                return parEmail;
            }
        }

        var nomHaut = nom.ToUpperInvariant();
        return await db.Parcours.Include(p => p.Candidat)
            .FirstOrDefaultAsync(p => p.Candidat!.Nom.ToUpper() == nomHaut
                                      && p.Candidat.Prenom.ToUpper() == prenom.ToUpper(), ct)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Rapproche une ligne du referentiel, du critere le plus fiable au moins
    /// fiable : code RNCP, puis abrege interne, puis debut d'intitule. Rend null
    /// plutot que de deviner : un mauvais rapprochement fausserait la grille
    /// tarifaire et les habilitations.
    /// </summary>
    private static CertificationRef? Rapprocher(
        List<CertificationRef> certifications, string? codeRncp, string? libelle)
    {
        if (certifications.Count == 0)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(codeRncp))
        {
            var chiffres = new string(codeRncp.Where(char.IsDigit).ToArray());
            if (chiffres.Length > 0)
            {
                var code = "RNCP" + chiffres;
                var parCode = certifications.FirstOrDefault(
                    c => string.Equals(c.CodeRncp, code, StringComparison.OrdinalIgnoreCase));
                if (parCode is not null)
                {
                    return parCode;
                }
            }
        }

        if (string.IsNullOrWhiteSpace(libelle))
        {
            return null;
        }

        var parAbrege = certifications.FirstOrDefault(
            c => !string.IsNullOrWhiteSpace(c.Abrege)
                 && libelle.Contains(c.Abrege!, StringComparison.OrdinalIgnoreCase));
        if (parAbrege is not null)
        {
            return parAbrege;
        }

        return certifications.FirstOrDefault(
            c => c.Intitule.Length > 12
                 && libelle.Contains(c.Intitule[..12], StringComparison.OrdinalIgnoreCase));
    }

    private static string? Champ(CsvReader csv, string? colonne) =>
        string.IsNullOrWhiteSpace(colonne) ? null
        : csv.TryGetField<string>(colonne, out var v) ? v : null;

    private static char Separateur(string ligne)
    {
        var pv = ligne.Count(c => c == ';');
        var vg = ligne.Count(c => c == ',');
        var tab = ligne.Count(c => c == '\t');
        if (tab > pv && tab > vg) return '\t';
        return pv >= vg ? ';' : ',';
    }

    private static DateOnly? Date(string? v)
    {
        if (string.IsNullOrWhiteSpace(v))
        {
            return null;
        }

        string[] formats = ["dd/MM/yyyy", "yyyy-MM-dd", "dd-MM-yyyy", "yyyy/MM/dd", "dd/MM/yy"];
        if (DateOnly.TryParseExact(v.Trim(), formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
        {
            return d;
        }

        return DateTime.TryParse(v, CultureInfo.GetCultureInfo("fr-FR"), DateTimeStyles.None, out var dt)
            ? DateOnly.FromDateTime(dt)
            : null;
    }

    private static string Capitaliser(string v) =>
        v.Length == 0 ? v : char.ToUpperInvariant(v[0]) + v[1..].ToLowerInvariant();
}
