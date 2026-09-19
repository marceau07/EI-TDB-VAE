using System.Globalization;
using EiVae.Domain.Entities;
using EiVae.Domain.Services;
using Microsoft.EntityFrameworkCore;

namespace EiVae.Infrastructure.Services;

/// <summary>
/// Fournit les règles d'alerte et les grilles à partir de la base, avec un cache
/// court : il évite d'interroger la base à chaque évaluation du moteur, et toute
/// modification l'invalide aussitôt.
/// </summary>
public sealed class ParametresService(VaeDbContext db)
{
    private static readonly TimeSpan DureeCache = TimeSpan.FromSeconds(60);

    private static List<RegleAlerte>? _regles;
    private static List<GrilleTarifaire>? _grilles;
    private static DateTimeOffset _expiration = DateTimeOffset.MinValue;
    private static readonly SemaphoreSlim Verrou = new(1, 1);

    public async Task<TarificationService> TarificationAsync(CancellationToken ct = default)
    {
        await ChargerAsync(ct).ConfigureAwait(false);
        return new TarificationService(_grilles!);
    }

    public async Task<IReadOnlyList<GrilleTarifaire>> GrillesAsync(CancellationToken ct = default)
    {
        await ChargerAsync(ct).ConfigureAwait(false);
        return _grilles!;
    }

    /// <summary>Toutes les règles, actives ou non : sert à libeller les alertes historisées.</summary>
    public async Task<IReadOnlyDictionary<string, RegleAlerte>> ReglesParCodeAsync(CancellationToken ct = default)
    {
        await ChargerAsync(ct).ConfigureAwait(false);
        return _regles!.ToDictionary(r => r.Code, StringComparer.Ordinal);
    }

    public async Task<MoteurAlertes> MoteurAsync(CancellationToken ct = default)
    {
        await ChargerAsync(ct).ConfigureAwait(false);
        return new MoteurAlertes(_regles!, new TarificationService(_grilles!));
    }

    /// <summary>Vide le cache après modification d'une règle ou d'une grille.</summary>
    public static void Invalider() => _expiration = DateTimeOffset.MinValue;

    public async Task<int> EntierAsync(string cle, int defaut, CancellationToken ct = default)
    {
        var p = await db.Parametres.AsNoTracking().FirstOrDefaultAsync(x => x.Cle == cle, ct)
            .ConfigureAwait(false);

        return p is not null && int.TryParse(p.Valeur, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)
            ? v
            : defaut;
    }

    private async Task ChargerAsync(CancellationToken ct)
    {
        if (_regles is not null && _grilles is not null && DateTimeOffset.UtcNow < _expiration)
        {
            return;
        }

        await Verrou.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_regles is not null && _grilles is not null && DateTimeOffset.UtcNow < _expiration)
            {
                return;
            }

            _regles = await db.ReglesAlertes.AsNoTracking()
                .OrderBy(r => r.Ordre).ThenBy(r => r.Code).ToListAsync(ct).ConfigureAwait(false);

            _grilles = await db.GrillesTarifaires.AsNoTracking()
                .OrderBy(g => g.DateEffet).ToListAsync(ct).ConfigureAwait(false);

            _expiration = DateTimeOffset.UtcNow.Add(DureeCache);
        }
        finally
        {
            Verrou.Release();
        }
    }
}
