using EiVae.Infrastructure.Integrations.FranceCompetences;
using EiVae.Infrastructure.Services;
using Microsoft.Extensions.Options;

namespace EiVae.Api.Services;

/// <summary>
/// Tâche de fond du service.
///
/// Deux cadences distinctes, parce que les besoins ne sont pas les mêmes :
/// le moteur d'alertes tourne toutes les heures, pour que le plan d'action du
/// matin soit juste ; la synchronisation France Compétences tourne une fois par
/// nuit, parce que l'export officiel n'est publié qu'une fois par jour et pèse
/// 75 Mo.
/// </summary>
public sealed class SynchronisationQuotidienne(
    IServiceScopeFactory scopes,
    IOptions<FranceCompetencesOptions> options,
    ILogger<SynchronisationQuotidienne> logger) : BackgroundService
{
    private static readonly TimeSpan CadenceAlertes = TimeSpan.FromHours(1);
    private DateOnly _derniereSynchroReferentiel = DateOnly.MinValue;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Laisse le temps à l'application de finir son démarrage et à la base
        // d'appliquer ses migrations avant la première passe.
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RafraichirAlertesAsync(stoppingToken).ConfigureAwait(false);
                await SynchroniserReferentielSiNecessaireAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                // Une passe en échec ne doit jamais arrêter la boucle : le service
                // réessaiera à la cadence suivante.
                logger.LogError(ex, "Passe de synchronisation en échec.");
            }

            try
            {
                await Task.Delay(CadenceAlertes, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task RafraichirAlertesAsync(CancellationToken ct)
    {
        await using var portee = scopes.CreateAsyncScope();
        var service = portee.ServiceProvider.GetRequiredService<ServiceAlertes>();
        await service.RafraichirAsync(ct).ConfigureAwait(false);
    }

    private async Task SynchroniserReferentielSiNecessaireAsync(CancellationToken ct)
    {
        var o = options.Value;
        if (!o.ActiverSynchronisationAutomatique)
        {
            return;
        }

        var maintenant = DateTime.Now;
        var aujourdHui = DateOnly.FromDateTime(maintenant);

        if (_derniereSynchroReferentiel == aujourdHui
            || TimeOnly.FromDateTime(maintenant) < o.HeureSynchronisation)
        {
            return;
        }

        await using var portee = scopes.CreateAsyncScope();
        var importeur = portee.ServiceProvider.GetRequiredService<FranceCompetencesImporter>();

        logger.LogInformation("France Compétences — démarrage de la synchronisation quotidienne.");
        var run = await importeur.SynchroniserAsync("planificateur", ct).ConfigureAwait(false);

        _derniereSynchroReferentiel = aujourdHui;
        logger.LogInformation(
            "France Compétences — {Statut} : {Crees} créées, {Majs} mises à jour, {Erreurs} erreurs.",
            run.Statut, run.NombreCrees, run.NombreMisAJour, run.NombreErreurs);
    }
}
