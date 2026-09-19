using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EiVae.Infrastructure.Integrations.FranceVae;

public sealed class FranceVaeOptions
{
    public const string Section = "FranceVae";

    /// <summary>Racine de l'API d'interopérabilité. Bac à sable ou production.</summary>
    public string BaseUrl { get; set; } = "https://vae.gouv.fr";

    /// <summary>Préfixe des routes de la version 1.</summary>
    public string Prefixe { get; set; } = "/interop/v1";

    /// <summary>Jeton d'accès, transmis en Authorization: Bearer.</summary>
    public string? Jeton { get; set; }

    /// <summary>Point de jeton Keycloak, pour le renouvellement automatique.</summary>
    public string? TokenUrl { get; set; }

    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }

    public bool EstConfigure => !string.IsNullOrWhiteSpace(BaseUrl)
                                && (!string.IsNullOrWhiteSpace(Jeton) || !string.IsNullOrWhiteSpace(ClientId));
}

/// <summary>Ce que l'API rend pour une candidature, réduit à ce que le tableau de bord exploite.</summary>
public sealed record CandidatureFranceVae(
    string Id,
    string? Nom,
    string? Prenom,
    string? Email,
    string? Telephone,
    string? CodeRncp,
    string? IntituleCertification,
    string? Statut,
    DateOnly? DateCandidature,
    string? Departement,
    string BrutJson);

/// <summary>
/// Client de l'API d'interopérabilité France VAE.
///
/// Portée réelle de cette API, vérifiée sur le dépôt public de la plateforme
/// (betagouv/reva, paquet reva-interop) : elle est décrite comme étant
/// « à destination des certificateurs » et n'expose, pour les candidatures,
/// qu'une lecture unitaire — GET /interop/v1/candidatures/{id}. Il n'existe pas
/// de route de listing permettant à un AAP de récupérer le flux de ses
/// candidatures entrantes.
///
/// Conséquence pour EI Groupe : ce client sert à rafraîchir un dossier dont on
/// connaît déjà l'identifiant. L'alimentation du flux entrant passe par
/// l'import de l'export du back-office France VAE — voir
/// <see cref="ImportCandidaturesCsv"/> — jusqu'à ouverture d'une route de
/// listing aux AAP.
/// </summary>
public sealed class FranceVaeClient(
    IHttpClientFactory httpClientFactory,
    IOptions<FranceVaeOptions> options,
    ILogger<FranceVaeClient> logger)
{
    private readonly FranceVaeOptions _options = options.Value;
    private string? _jetonCache;
    private DateTimeOffset _jetonExpire = DateTimeOffset.MinValue;

    public bool EstConfigure => _options.EstConfigure;

    /// <summary>Vérifie que l'API répond et que le jeton est accepté.</summary>
    public async Task<(bool Ok, string Message)> TesterConnexionAsync(CancellationToken ct = default)
    {
        if (!_options.EstConfigure)
        {
            return (false, "Aucun jeton ni identifiant client n'est configuré pour France VAE.");
        }

        try
        {
            var http = await ClientAsync(ct).ConfigureAwait(false);

            // Une candidature inexistante répond 204 quand le jeton est valide,
            // et 401 quand il ne l'est pas : c'est le contrôle le moins invasif.
            using var reponse = await http
                .GetAsync($"{_options.Prefixe}/candidatures/00000000-0000-0000-0000-000000000000", ct)
                .ConfigureAwait(false);

            return reponse.StatusCode switch
            {
                HttpStatusCode.Unauthorized =>
                    (false, "France VAE a refusé le jeton (401). Vérifiez sa validité et son périmètre."),
                HttpStatusCode.Forbidden =>
                    (false, "Jeton valide mais périmètre insuffisant (403) : l'accès aux candidatures n'est pas ouvert à ce compte."),
                HttpStatusCode.NotFound =>
                    (false, $"Route introuvable (404). Vérifiez l'URL de base et le préfixe « {_options.Prefixe} »."),
                _ => (true, $"Connexion établie ({(int)reponse.StatusCode}).")
            };
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "France VAE — test de connexion en échec");
            return (false, $"Connexion impossible : {ex.Message}");
        }
    }

    /// <summary>
    /// Lit une candidature par son identifiant. Rend null si France VAE répond
    /// 204, ce qui signifie « candidature introuvable » dans cette API.
    /// </summary>
    public async Task<CandidatureFranceVae?> ObtenirCandidatureAsync(string id, CancellationToken ct = default)
    {
        var http = await ClientAsync(ct).ConfigureAwait(false);

        using var reponse = await http.GetAsync($"{_options.Prefixe}/candidatures/{Uri.EscapeDataString(id)}", ct)
            .ConfigureAwait(false);

        if (reponse.StatusCode == HttpStatusCode.NoContent)
        {
            return null;
        }

        reponse.EnsureSuccessStatusCode();

        var brut = await reponse.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(brut);

        // L'API enveloppe sa réponse dans « data ».
        var racine = doc.RootElement.TryGetProperty("data", out var data) ? data : doc.RootElement;

        return new CandidatureFranceVae(
            Id: Lire(racine, "id") ?? id,
            Nom: Lire(racine, "candidat", "nom") ?? Lire(racine, "nom"),
            Prenom: Lire(racine, "candidat", "prenom") ?? Lire(racine, "prenom"),
            Email: Lire(racine, "candidat", "email") ?? Lire(racine, "email"),
            Telephone: Lire(racine, "candidat", "telephone") ?? Lire(racine, "telephone"),
            CodeRncp: Lire(racine, "certification", "codeRncp") ?? Lire(racine, "codeRncp"),
            IntituleCertification: Lire(racine, "certification", "libelle")
                                   ?? Lire(racine, "certification", "intitule"),
            Statut: Lire(racine, "statut") ?? Lire(racine, "status"),
            DateCandidature: DateOnly.TryParse(Lire(racine, "dateCandidature") ?? Lire(racine, "createdAt"), out var d)
                ? d
                : null,
            Departement: Lire(racine, "candidat", "departement", "code") ?? Lire(racine, "departement"),
            BrutJson: brut);
    }

    private static string? Lire(JsonElement racine, params string[] chemin)
    {
        var courant = racine;
        foreach (var nom in chemin)
        {
            if (courant.ValueKind != JsonValueKind.Object || !courant.TryGetProperty(nom, out courant))
            {
                return null;
            }
        }

        return courant.ValueKind switch
        {
            JsonValueKind.String => courant.GetString(),
            JsonValueKind.Number => courant.ToString(),
            _ => null,
        };
    }

    private async Task<HttpClient> ClientAsync(CancellationToken ct)
    {
        var http = httpClientFactory.CreateClient(nameof(FranceVaeClient));
        http.BaseAddress = new Uri(_options.BaseUrl.TrimEnd('/'));

        var jeton = await JetonAsync(ct).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(jeton))
        {
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jeton);
        }

        return http;
    }

    /// <summary>
    /// Jeton courant. Si un couple client/secret Keycloak est fourni, le jeton
    /// est renouvelé automatiquement ; sinon on utilise le jeton statique.
    /// </summary>
    private async Task<string?> JetonAsync(CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(_options.Jeton))
        {
            return _options.Jeton;
        }

        if (string.IsNullOrWhiteSpace(_options.TokenUrl) || string.IsNullOrWhiteSpace(_options.ClientId))
        {
            return null;
        }

        if (_jetonCache is not null && DateTimeOffset.UtcNow < _jetonExpire)
        {
            return _jetonCache;
        }

        var http = httpClientFactory.CreateClient(nameof(FranceVaeClient));
        using var contenu = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = _options.ClientId!,
            ["client_secret"] = _options.ClientSecret ?? string.Empty,
        });

        using var reponse = await http.PostAsync(_options.TokenUrl, contenu, ct).ConfigureAwait(false);
        reponse.EnsureSuccessStatusCode();

        var jeton = await reponse.Content.ReadFromJsonAsync<ReponseJeton>(ct).ConfigureAwait(false);
        if (jeton?.AccessToken is null)
        {
            return null;
        }

        _jetonCache = jeton.AccessToken;
        // Marge de sécurité : on renouvelle une minute avant l'expiration annoncée.
        _jetonExpire = DateTimeOffset.UtcNow.AddSeconds(Math.Max(30, jeton.ExpiresIn - 60));
        return _jetonCache;
    }

    private sealed record ReponseJeton(
        [property: System.Text.Json.Serialization.JsonPropertyName("access_token")] string? AccessToken,
        [property: System.Text.Json.Serialization.JsonPropertyName("expires_in")] int ExpiresIn);
}
