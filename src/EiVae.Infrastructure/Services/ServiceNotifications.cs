using System.Globalization;
using System.Text.Json;
using EiVae.Domain;
using EiVae.Domain.Entities;
using EiVae.Domain.Services;
using EiVae.Infrastructure.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EiVae.Infrastructure.Services;

/// <summary>État d'un dossier avant modification, pour détecter ce qui vient de changer.</summary>
public sealed record EtatDossier(
    int? AapId, int? AccompagnateurId, EtapeParcours Etape, DateOnly? DateParcoursValide, DateOnly? DateRecevabilite)
{
    public static EtatDossier De(Parcours p) =>
        new(p.AapId, p.AccompagnateurId, p.Etape, p.DateParcoursValide, p.DateRecevabilite);

    /// <summary>Recevabilité validée : date de recevabilité saisie, ou étape E6 atteinte.</summary>
    public bool EstRecevable =>
        DateRecevabilite is not null
        || (Etape >= EtapeParcours.Recevabilite && Etape != EtapeParcours.Sortie);

    /// <summary>Un parcours est prescrit dès sa validation : jalon daté, ou étape atteinte.</summary>
    public bool EstPrescrit =>
        DateParcoursValide is not null
        || (Etape >= EtapeParcours.ParcoursValide && Etape != EtapeParcours.Sortie);
}

/// <summary>
/// Émet les notifications liées au cycle de vie d'un dossier :
/// création (rôles abonnés), affectation d'un AAP ou d'un accompagnateur
/// (l'intervenant concerné), recevabilité validée et prescription du parcours
/// (rôles abonnés).
///
/// Les destinataires par rôle se règlent dans la matrice des droits. Un rôle
/// abonné sans utilisateur reçoit la notification à son nom : elle reste
/// visible dans l'application, faute d'adresse à qui l'envoyer.
/// </summary>
public sealed class ServiceNotifications(
    VaeDbContext db,
    ParametresService parametres,
    GenerateurDossierCandidat generateur,
    ILogger<ServiceNotifications> logger)
{
    private static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");

    /// <summary>
    /// Compare le dossier enregistré à son état antérieur et notifie ce qui a changé.
    /// <paramref name="avant"/> vaut null pour un dossier qui vient d'être créé.
    /// </summary>
    public async Task<int> NotifierAsync(int parcoursId, EtatDossier? avant, CancellationToken ct = default)
    {
        var p = await db.Parcours
            .Include(x => x.Candidat)
            .Include(x => x.Certification)
            .Include(x => x.Aap)
            .Include(x => x.Accompagnateur)
            .Include(x => x.Financements)
            .FirstOrDefaultAsync(x => x.Id == parcoursId, ct).ConfigureAwait(false);

        if (p is null)
        {
            return 0;
        }

        var apres = EtatDossier.De(p);
        var notifications = new List<Notification>();
        var candidat = $"{p.Candidat?.Prenom} {p.Candidat?.Nom}".Trim();
        var certification = LibelleCertification(p);

        if (avant is null)
        {
            notifications.AddRange(await PourRolesAsync(EvenementNotification.CandidatCree, p,
                $"Nouveau candidat : {candidat}",
                $"Dossier créé le {DateTime.Now:dd/MM/yyyy} pour {certification}. "
                + $"Origine : {LibelleOrigine(p.Origine)}. "
                + $"AAP référent : {p.Aap?.NomComplet ?? "non affecté"}.", ct).ConfigureAwait(false));
        }

        if (p.Aap is { } aap && p.AapId != avant?.AapId)
        {
            notifications.Add(PourIntervenant(EvenementNotification.AapAffecte, p, aap,
                $"Vous êtes AAP référent de {candidat}",
                $"Le dossier de {candidat} ({certification}) vous a été confié. "
                + $"Étape : {MoteurAlertes.Libelle(p.Etape)}. {Contact(p)}"));
        }

        if (p.Accompagnateur is { } acc && p.AccompagnateurId != avant?.AccompagnateurId)
        {
            notifications.Add(PourIntervenant(EvenementNotification.AccompagnateurAffecte, p, acc,
                $"Nouvel accompagnement : {candidat}",
                $"Vous accompagnez {candidat} vers {certification}. "
                + $"AAP référent : {p.Aap?.NomComplet ?? "non affecté"}. {Contact(p)}"));
        }

        // Recevabilité validée : le digital learning reçoit la fiche Word, la
        // coordination est invitée à attribuer un accompagnateur s'il n'y en a pas.
        // Une seule notification par dossier et par événement.
        if (apres.EstRecevable && avant?.EstRecevable != true)
        {
            if (!await DejaNotifieAsync(p.Id, EvenementNotification.RecevabiliteValidee, ct).ConfigureAwait(false))
            {
                var (url, libelle) = generateur.LienFiche(p);
                notifications.AddRange(await PourRolesAsync(EvenementNotification.RecevabiliteValidee, p,
                    $"Recevabilité validée : {candidat}",
                    $"La recevabilité de {candidat} est validée"
                    + (p.DateRecevabilite is { } d ? $" le {d:dd/MM/yyyy}" : string.Empty)
                    + $" pour {certification}. Accompagnateur : {p.Accompagnateur?.NomComplet ?? "à attribuer"}. "
                    + "La fiche Word du candidat est jointe par le lien ci-dessous.", ct,
                    url, libelle).ConfigureAwait(false));
            }

            if (p.AccompagnateurId is null
                && !await DejaNotifieAsync(p.Id, EvenementNotification.AccompagnateurAAttribuer, ct).ConfigureAwait(false))
            {
                notifications.AddRange(await PourRolesAsync(EvenementNotification.AccompagnateurAAttribuer, p,
                    $"Accompagnateur à attribuer : {candidat}",
                    $"La recevabilité de {candidat} est validée pour {certification}. "
                    + $"AAP référent : {p.Aap?.NomComplet ?? "non affecté"}. "
                    + "Attribuez un accompagnateur habilité depuis la fiche du candidat.", ct,
                    $"/#fiche-{p.Id}", "Ouvrir la fiche du candidat").ConfigureAwait(false));
            }
        }

        // Une seule notification de prescription par dossier, même si l'étape
        // recule puis avance de nouveau.
        if (apres.EstPrescrit && avant?.EstPrescrit != true
            && !await db.Notifications.AnyAsync(
                n => n.ParcoursId == p.Id && n.Evenement == EvenementNotification.ParcoursPrescrit, ct)
                .ConfigureAwait(false))
        {
            var tarification = await parametres.TarificationAsync(ct).ConfigureAwait(false);
            var calcul = tarification.Calculer(p);
            var grille = tarification.GrillePour(p);
            var financement = p.Financements.FirstOrDefault(f => f.EstSecurise) ?? p.Financements.FirstOrDefault();

            notifications.AddRange(await PourRolesAsync(EvenementNotification.ParcoursPrescrit, p,
                $"Parcours prescrit : {candidat}",
                $"{certification}. Grille {grille.Code}, montant prescrit {calcul.Total.ToString("N0", Fr).Replace('\u202f', ' ')} € HT "
                + $"({p.HeuresIndividuelPrescrites:0.#} h individuel, {p.HeuresCollectifPrescrites:0.#} h collectif, "
                + $"{p.HeuresComplementFormatifPrescrites:0.#} h de compléments). "
                + $"Financement : {(financement is null ? "non renseigné" : (financement.Financeur ?? financement.Dispositif.ToString()) + (financement.EstSecurise ? ", sécurisé" : ", non sécurisé"))}. "
                + $"Code ACF : {p.CodeAcfSolei ?? "non créé"}.", ct).ConfigureAwait(false));
        }

        if (notifications.Count == 0)
        {
            return 0;
        }

        db.Notifications.AddRange(notifications);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        logger.LogInformation("Dossier {Parcours} : {Nombre} notification(s) émise(s)", p.Id, notifications.Count);
        return notifications.Count;
    }

    /// <summary>
    /// Un import crée les dossiers par lots : une notification récapitulative
    /// par destinataire plutôt qu'une par candidat.
    /// </summary>
    public async Task<int> NotifierImportAsync(IReadOnlyList<int> parcoursIds, string source, CancellationToken ct = default)
    {
        if (parcoursIds.Count == 0)
        {
            return 0;
        }

        if (parcoursIds.Count == 1)
        {
            return await NotifierAsync(parcoursIds[0], null, ct).ConfigureAwait(false);
        }

        var noms = await db.Parcours.Where(p => parcoursIds.Contains(p.Id))
            .Select(p => p.Candidat!.Prenom + " " + p.Candidat.Nom)
            .ToListAsync(ct).ConfigureAwait(false);

        var notifications = await PourRolesAsync(EvenementNotification.CandidatCree, null,
            $"{parcoursIds.Count} nouveaux candidats ({source})",
            string.Join(", ", noms.Take(40)) + (noms.Count > 40 ? $" et {noms.Count - 40} autres." : "."),
            ct).ConfigureAwait(false);

        db.Notifications.AddRange(notifications);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return notifications.Count;
    }

    private Task<bool> DejaNotifieAsync(int parcoursId, EvenementNotification evenement, CancellationToken ct) =>
        db.Notifications.AnyAsync(n => n.ParcoursId == parcoursId && n.Evenement == evenement, ct);

    private async Task<List<Notification>> PourRolesAsync(
        EvenementNotification evenement, Parcours? p, string titre, string message, CancellationToken ct,
        string? lien = null, string? libelleLien = null)
    {
        var code = evenement.ToString();
        var roles = (await db.RolesAcces.Include(r => r.Utilisateurs).AsNoTracking()
                .ToListAsync(ct).ConfigureAwait(false))
            .Where(r => Evenements(r.NotificationsJson).Contains(code))
            .ToList();

        var sortie = new List<Notification>();
        foreach (var role in roles)
        {
            var membres = role.Utilisateurs.Where(u => u.Actif).ToList();
            if (membres.Count == 0)
            {
                sortie.Add(new Notification
                {
                    Evenement = evenement, Titre = titre, Message = message, ParcoursId = p?.Id,
                    RoleAccesId = role.Id, DestinataireNom = role.Nom, Lien = lien, LibelleLien = libelleLien,
                });
                continue;
            }

            sortie.AddRange(membres.Select(u => new Notification
            {
                Evenement = evenement, Titre = titre, Message = message, ParcoursId = p?.Id,
                RoleAccesId = role.Id, UtilisateurId = u.Id,
                DestinataireNom = u.NomComplet, DestinataireEmail = u.Email, Lien = lien, LibelleLien = libelleLien,
            }));
        }

        return sortie;
    }

    private static Notification PourIntervenant(
        EvenementNotification evenement, Parcours p, Intervenant i, string titre, string message) => new()
    {
        Evenement = evenement, Titre = titre, Message = message, ParcoursId = p.Id,
        IntervenantId = i.Id, DestinataireNom = i.NomComplet, DestinataireEmail = i.Email,
    };

    /// <summary>Événements auxquels un rôle est abonné, tolérant à un JSON mal formé.</summary>
    public static IReadOnlyList<string> Evenements(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public static string LibelleEvenement(EvenementNotification e) => e switch
    {
        EvenementNotification.CandidatCree => "Création d'un candidat",
        EvenementNotification.AapAffecte => "Affectation de l'AAP référent",
        EvenementNotification.AccompagnateurAffecte => "Affectation de l'accompagnateur",
        EvenementNotification.ParcoursPrescrit => "Parcours prescrit",
        EvenementNotification.RecevabiliteValidee => "Recevabilité validée",
        EvenementNotification.AccompagnateurAAttribuer => "Accompagnateur à attribuer",
        _ => e.ToString(),
    };

    public static string LibelleOrigine(OrigineCandidature o) => o switch
    {
        OrigineCandidature.FranceVae => "France VAE",
        OrigineCandidature.SiteWeb => "site groupe-ei.fr",
        OrigineCandidature.EntreeDirecte => "entrée directe",
        OrigineCandidature.Prescripteur => "prescripteur",
        OrigineCandidature.VaeCollective => "VAE collective",
        OrigineCandidature.Import => "import",
        _ => o.ToString(),
    };

    private static string LibelleCertification(Parcours p) =>
        p.Certification is null
            ? "certification à déterminer"
            : (p.Certification.Abrege is { Length: > 0 } a ? $"{a} — " : string.Empty) + p.Certification.Intitule;

    private static string Contact(Parcours p)
    {
        var elements = new[] { p.Candidat?.Email, p.Candidat?.Telephone }.Where(x => !string.IsNullOrWhiteSpace(x));
        var contact = string.Join(" · ", elements);
        return contact.Length == 0 ? "Coordonnées du candidat non renseignées." : $"Contact : {contact}.";
    }
}
