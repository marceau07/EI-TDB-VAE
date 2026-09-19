using EiVae.Domain;
using EiVae.Domain.Entities;
using EiVae.Domain.Services;
using EiVae.Infrastructure;
using EiVae.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace EiVae.Api.Services;

/// <summary>Filtres transverses appliqués à toutes les vues du tableau de bord.</summary>
public sealed record FiltreTableauDeBord(
    int? Mois = null,
    int? AapId = null,
    int? CertificationId = null,
    int? AccompagnateurId = null,
    DispositifFinancement? Financeur = null,
    string? Region = null,
    EtapeParcours? Etape = null,
    string? Recherche = null);

public sealed record LigneAlerte(
    long Id, string Code, string Severite, string Libelle, string ActionAttendue, string? Responsable,
    string? Fondement, string Detail, int ParcoursId, string Candidat, string? Certification,
    string Etape, string? Aap, string Financement, string? Region, DateOnly? DernierMouvement,
    DateTimeOffset DetecteeLe, TypeCondition Condition);

public sealed record LigneParcours(
    int Id, int CandidatId, string Nom, string Prenom, string? Certification, string? CodeRncp,
    int? Niveau, string? Domaine, string Etape, int EtapeRang, string? StatutSecondaire, string? Sortie,
    string? Aap, string? Accompagnateur, string Financement, decimal HeuresPrescrites,
    decimal HeuresRealisees, decimal Montant, decimal Marge, string CodeGrille,
    DateOnly? DateDemande, DateOnly? DateDebut, DateOnly? DateJury, DateOnly? DernierMouvement,
    int NbAlertes, string Severite, string? Ville, string? Region, string? UrlSharePoint);

public sealed record SyntheseDirection(
    int DossiersEntrantsMois, int DossiersActifs, int DossiersClotures, int Sorties,
    int EnQualification, int EnFaisabilite, int EnAccompagnement, int JurysAVenir, int CertificationsObtenues,
    int? DelaiPriseEnCharge, int? DureeMoyenneParcoursJours, decimal TauxSortie,
    decimal TauxValidationTotale, int ResultatsNonRenseignes, int DossiersConformes,
    decimal CaPrevisionnel, decimal CaFacture, decimal CaEncaisse, decimal MargePrevisionnelle,
    decimal PanierMoyen, int NbAap, decimal ChargeMoyenneAap, int CapaciteDisponible, int AapEnSurcharge,
    int AlertesCritiques, int AlertesVigilance, int FinancementsNonSecurises, int DossiersSansActivite,
    int PrestationsNonFacturees, decimal MontantNonFacture,
    IReadOnlyList<PointMensuel> EvolutionDemandes, IReadOnlyList<RepartitionEtape> RepartitionPipeline);

public sealed record PointMensuel(string Mois, int Annee, int Valeur, decimal Montant);
public sealed record RepartitionEtape(string Etape, int Rang, int Nombre);

/// <summary>
/// Agrégations servies au tableau de bord. Tout est calculé en base, jamais
/// dans le navigateur : les chiffres présentés en direction doivent être les
/// mêmes que ceux que voit l'AAP dans son plan d'action.
/// </summary>
public sealed class ServiceTableauDeBord(
    VaeDbContext db,
    ParametresService parametres,
    SharePointLinkBuilder sharePoint)
{
    public IQueryable<Parcours> Requete(FiltreTableauDeBord f)
    {
        var q = db.Parcours
            .Include(p => p.Candidat)
            .Include(p => p.Certification)
            .Include(p => p.Aap)
            .Include(p => p.Accompagnateur)
            .Include(p => p.Financements)
            .Include(p => p.Factures)
            .Include(p => p.Seances)
            .Include(p => p.Alertes.Where(a => a.ResolueLe == null))
            .AsQueryable();

        if (f.AapId is { } aap)
        {
            q = q.Where(p => p.AapId == aap);
        }

        if (f.CertificationId is { } cert)
        {
            q = q.Where(p => p.CertificationId == cert);
        }

        if (f.AccompagnateurId is { } acc)
        {
            q = q.Where(p => p.AccompagnateurId == acc);
        }

        if (f.Etape is { } etape)
        {
            q = q.Where(p => p.Etape == etape);
        }

        if (!string.IsNullOrWhiteSpace(f.Region))
        {
            q = q.Where(p => p.Candidat!.Region == f.Region);
        }

        if (f.Financeur is { } fin)
        {
            q = fin == DispositifFinancement.NonSecurise
                ? q.Where(p => !p.Financements.Any(x => x.DateSecurisation != null))
                : q.Where(p => p.Financements.Any(x => x.Dispositif == fin));
        }

        if (f.Mois is { } mois and > 0)
        {
            var limite = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-mois));
            q = q.Where(p => p.DateDemande != null && p.DateDemande >= limite);
        }

        if (!string.IsNullOrWhiteSpace(f.Recherche))
        {
            var r = f.Recherche.Trim();
            q = q.Where(p =>
                EF.Functions.ILike(p.Candidat!.Nom, $"%{r}%")
                || EF.Functions.ILike(p.Candidat.Prenom, $"%{r}%")
                || (p.Certification != null && EF.Functions.ILike(p.Certification.Intitule, $"%{r}%"))
                || (p.Candidat.Ville != null && EF.Functions.ILike(p.Candidat.Ville, $"%{r}%")));
        }

        return q;
    }

    public async Task<IReadOnlyList<LigneParcours>> ListerAsync(FiltreTableauDeBord f, CancellationToken ct)
    {
        var tarification = await parametres.TarificationAsync(ct).ConfigureAwait(false);
        var parcours = await Requete(f).AsSplitQuery().ToListAsync(ct).ConfigureAwait(false);
        return parcours.Select(p => Projeter(p, tarification)).ToList();
    }

    public LigneParcours Projeter(Parcours p, TarificationService tarification)
    {
        var calcul = tarification.Calculer(p);
        var realisees = p.Seances.Where(s => s.Realisee && s.Nature != NatureHeure.Asynchrone)
            .Sum(s => s.DureeHeures);

        var critiques = p.Alertes.Count(a => a.EstOuverte && a.Severite == SeveriteAlerte.Critique);
        var vigilance = p.Alertes.Count(a => a.EstOuverte && a.Severite == SeveriteAlerte.Vigilance);

        var severite = critiques > 0 ? "critique"
            : vigilance > 0 ? "vigilance"
            : p.EstActif ? "conforme" : "neutre";

        var financement = p.Financements.FirstOrDefault(x => x.EstSecurise);

        return new LigneParcours(
            p.Id, p.CandidatId, p.Candidat?.Nom ?? "", p.Candidat?.Prenom ?? "",
            p.Certification?.Abrege ?? p.Certification?.Intitule, p.Certification?.CodeRncp,
            p.Certification?.Niveau, p.Certification?.DomaineEi,
            MoteurAlertes.Libelle(p.Etape), (int)p.Etape,
            p.StatutSecondaire == StatutSecondaire.Aucun ? null : LibelleStatut(p.StatutSecondaire),
            p.MotifSortie == MotifSortie.Aucun ? null : LibelleSortie(p.MotifSortie),
            p.Aap?.NomComplet, p.Accompagnateur?.NomComplet,
            financement is null ? "Non sécurisé" : LibelleDispositif(financement.Dispositif),
            p.HeuresPrescritesTotales, realisees, calcul.Total, calcul.Marge, calcul.CodeGrille,
            p.DateDemande, p.DateDebutParcours, p.DateJury, p.DateDernierMouvement,
            critiques + vigilance, severite,
            p.Candidat?.Ville, p.Candidat?.Region,
            sharePoint.EstConfigure ? sharePoint.UrlDossierCandidat(p) : null);
    }

    public async Task<SyntheseDirection> SyntheseAsync(FiltreTableauDeBord f, CancellationToken ct)
    {
        var tarification = await parametres.TarificationAsync(ct).ConfigureAwait(false);
        var capaciteAap = await parametres.EntierAsync("capacite.aap", 12, ct).ConfigureAwait(false);

        var parcours = await Requete(f).AsSplitQuery().ToListAsync(ct).ConfigureAwait(false);
        var aujourdHui = DateOnly.FromDateTime(DateTime.UtcNow);
        var debutMois = new DateOnly(aujourdHui.Year, aujourdHui.Month, 1);

        var actifs = parcours.Where(p => p.EstActif).ToList();
        var clotures = parcours.Where(p => p.Etape == EtapeParcours.Cloture).ToList();
        var sorties = parcours.Where(p => p.Etape == EtapeParcours.Sortie).ToList();

        var alertes = actifs.SelectMany(p => p.Alertes.Where(a => a.EstOuverte)).ToList();

        // Les indicateurs s'appuient sur la nature des règles, pas sur leur code :
        // une règle renumérotée ou ajoutée par le service y est comptée.
        var regles = (await parametres.ReglesParCodeAsync(ct).ConfigureAwait(false)).Values.ToList();
        var codesFinancement = regles.Where(r => r.Condition == TypeCondition.FinancementNonSecurise)
            .Select(r => r.Code).ToHashSet(StringComparer.Ordinal);
        var codesInactivite = regles.Where(r => r.Condition == TypeCondition.DelaiDepuisJalon
                                                && r.JalonReference == nameof(Parcours.DateDernierMouvement)
                                                && r.SeuilMax is null)
            .Select(r => r.Code).ToHashSet(StringComparer.Ordinal);
        var postJuryNonFactures = parcours
            .Where(p => p.Etape == EtapeParcours.PostJury && p.Factures.Count == 0).ToList();

        // Délais : mesurés uniquement là où les deux bornes existent.
        var delaisPriseEnCharge = parcours
            .Where(p => p.DateDemande is not null && p.DatePremierContact is not null)
            .Select(p => p.DatePremierContact!.Value.DayNumber - p.DateDemande!.Value.DayNumber)
            .Where(j => j >= 0).ToList();

        var dureesTotales = clotures
            .Where(p => p.DateDemande is not null && (p.DateJury ?? p.DateEntretienPostJury) is not null)
            .Select(p => ((p.DateJury ?? p.DateEntretienPostJury)!.Value.DayNumber - p.DateDemande!.Value.DayNumber))
            .Where(j => j > 0).ToList();

        var juryPasses = parcours.Count(p => p.ResultatJury != ResultatJury.NonRenseigne
                                             || (p.DateJury is not null && p.DateJury <= aujourdHui));
        var validationsTotales = parcours.Count(p => p.ResultatJury == ResultatJury.ValidationTotale);
        var nonRenseignes = parcours.Count(p => p.ResultatJury == ResultatJury.NonRenseigne
                                                && p.DateJury is not null && p.DateJury <= aujourdHui);

        var chargeParAap = actifs.Where(p => p.AapId is not null)
            .GroupBy(p => p.AapId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        var nbAap = await db.Intervenants
            .CountAsync(i => i.Type == TypeIntervenant.ArchitecteAccompagnateurParcours
                             && (i.Statut == StatutIntervenant.Actif || i.Statut == StatutIntervenant.EnIntegration), ct)
            .ConfigureAwait(false);

        var caPrevisionnel = actifs.Sum(p => tarification.Calculer(p).Total);
        var margePrevisionnelle = actifs.Sum(p => tarification.Calculer(p).Marge);

        var toutesFactures = await db.Factures.AsNoTracking().ToListAsync(ct).ConfigureAwait(false);
        var caFacture = toutesFactures.Sum(x => x.MontantHt);
        var caEncaisse = toutesFactures.Where(x => x.EstReglee).Sum(x => x.MontantHt);

        // Séries mensuelles sur dix-huit mois.
        var evolution = new List<PointMensuel>();
        for (var i = 17; i >= 0; i--)
        {
            var d = DateTime.UtcNow.AddMonths(-i);
            var debut = new DateOnly(d.Year, d.Month, 1);
            var fin = debut.AddMonths(1);
            evolution.Add(new PointMensuel(
                MoisCourt(d.Month), d.Year,
                parcours.Count(p => p.DateDemande >= debut && p.DateDemande < fin),
                toutesFactures.Where(x => x.DateEmission >= debut && x.DateEmission < fin).Sum(x => x.MontantHt)));
        }

        var repartition = actifs.GroupBy(p => p.Etape)
            .OrderBy(g => (int)g.Key)
            .Select(g => new RepartitionEtape(MoteurAlertes.Libelle(g.Key), (int)g.Key, g.Count()))
            .ToList();

        return new SyntheseDirection(
            DossiersEntrantsMois: parcours.Count(p => p.DateDemande >= debutMois),
            DossiersActifs: actifs.Count,
            DossiersClotures: clotures.Count,
            Sorties: sorties.Count,
            EnQualification: actifs.Count(p => p.Etape <= EtapeParcours.RecueilDesBesoins),
            EnFaisabilite: actifs.Count(p => p.Etape is >= EtapeParcours.Financement and <= EtapeParcours.ParcoursValide),
            EnAccompagnement: actifs.Count(p => p.Etape is EtapeParcours.Accompagnement or EtapeParcours.PreparationJury),
            JurysAVenir: actifs.Count(p => p.DateJury > aujourdHui),
            CertificationsObtenues: validationsTotales,
            DelaiPriseEnCharge: delaisPriseEnCharge.Count > 0 ? (int)delaisPriseEnCharge.Average() : null,
            DureeMoyenneParcoursJours: dureesTotales.Count > 0 ? (int)dureesTotales.Average() : null,
            TauxSortie: Ratio(sorties.Count, parcours.Count),
            TauxValidationTotale: Ratio(validationsTotales, juryPasses),
            ResultatsNonRenseignes: nonRenseignes,
            DossiersConformes: actifs.Count(p => !p.Alertes.Any(a => a.EstOuverte)),
            CaPrevisionnel: caPrevisionnel,
            CaFacture: caFacture,
            CaEncaisse: caEncaisse,
            MargePrevisionnelle: margePrevisionnelle,
            PanierMoyen: toutesFactures.Count > 0 ? Math.Round(caFacture / toutesFactures.Count, 2) : 0,
            NbAap: nbAap,
            ChargeMoyenneAap: nbAap > 0 ? Math.Round((decimal)actifs.Count / nbAap, 1) : 0,
            CapaciteDisponible: Math.Max(0, nbAap * capaciteAap - actifs.Count),
            AapEnSurcharge: chargeParAap.Count(kv => kv.Value > capaciteAap),
            AlertesCritiques: alertes.Count(a => a.Severite == SeveriteAlerte.Critique),
            AlertesVigilance: alertes.Count(a => a.Severite == SeveriteAlerte.Vigilance),
            FinancementsNonSecurises: alertes.Count(a => codesFinancement.Contains(a.CodeRegle)),
            DossiersSansActivite: alertes.Where(a => codesInactivite.Contains(a.CodeRegle))
                .Select(a => a.ParcoursId).Distinct().Count(),
            PrestationsNonFacturees: postJuryNonFactures.Count,
            MontantNonFacture: postJuryNonFactures.Sum(p => tarification.Calculer(p).Total),
            EvolutionDemandes: evolution,
            RepartitionPipeline: repartition);
    }

    public async Task<IReadOnlyList<LigneAlerte>> AlertesAsync(FiltreTableauDeBord f, CancellationToken ct)
    {
        var parcours = await Requete(f).AsSplitQuery().ToListAsync(ct).ConfigureAwait(false);
        var regles = await parametres.ReglesParCodeAsync(ct).ConfigureAwait(false);
        var lignes = new List<LigneAlerte>();

        foreach (var p in parcours)
        {
            foreach (var a in p.Alertes.Where(x => x.EstOuverte))
            {
                if (a.ReporteeJusquA is { } report && report > DateOnly.FromDateTime(DateTime.UtcNow))
                {
                    continue;
                }

                if (!regles.TryGetValue(a.CodeRegle, out var regle))
                {
                    continue;
                }

                lignes.Add(new LigneAlerte(
                    a.Id, a.CodeRegle,
                    a.Severite == SeveriteAlerte.Critique ? "critique" : "vigilance",
                    regle.Libelle, regle.ActionAttendue, regle.Responsable, regle.Fondement,
                    a.Detail ?? string.Empty, p.Id,
                    $"{p.Candidat?.Prenom} {p.Candidat?.Nom}".Trim(),
                    p.Certification?.Abrege ?? p.Certification?.Intitule,
                    MoteurAlertes.Libelle(p.Etape), p.Aap?.NomComplet,
                    p.Financements.Any(x => x.EstSecurise) ? "sécurisé" : "non sécurisé",
                    p.Candidat?.Region, p.DateDernierMouvement, a.DetecteeLe, regle.Condition));
            }
        }

        return lignes
            .OrderBy(l => l.Severite == "critique" ? 0 : 1)
            .ThenBy(l => l.DernierMouvement ?? DateOnly.MaxValue)
            .ToList();
    }

    private static decimal Ratio(int n, int d) => d == 0 ? 0 : Math.Round((decimal)n / d, 4);

    private static string MoisCourt(int m) => m switch
    {
        1 => "janv.", 2 => "févr.", 3 => "mars", 4 => "avr.", 5 => "mai", 6 => "juin",
        7 => "juil.", 8 => "août", 9 => "sept.", 10 => "oct.", 11 => "nov.", _ => "déc.",
    };

    public static string LibelleStatut(StatutSecondaire s) => s switch
    {
        StatutSecondaire.AttenteCandidat => "En attente candidat",
        StatutSecondaire.AttenteFranceVae => "En attente France VAE",
        StatutSecondaire.AttenteFinanceur => "En attente financeur",
        StatutSecondaire.AttenteCertificateur => "En attente certificateur",
        StatutSecondaire.Bloque => "Bloqué",
        StatutSecondaire.Reoriente => "Réorienté",
        _ => "",
    };

    public static string LibelleSortie(MotifSortie m) => m switch
    {
        MotifSortie.AbandonCandidat => "Abandon candidat",
        MotifSortie.AbandonAap => "Abandon AAP",
        MotifSortie.DisparuFranceVae => "Disparu de France VAE",
        MotifSortie.CandidatureSupprimee => "Candidature supprimée",
        MotifSortie.Reorientation => "Réorientation",
        MotifSortie.FinancementNonObtenu => "Financement non obtenu",
        _ => "",
    };

    public static string LibelleDispositif(DispositifFinancement d) => d switch
    {
        DispositifFinancement.Cpf => "CPF",
        DispositifFinancement.Opco => "OPCO",
        DispositifFinancement.Employeur => "Employeur",
        DispositifFinancement.FranceTravail => "France Travail",
        DispositifFinancement.TransitionPro => "Transition Pro",
        DispositifFinancement.Region => "Région",
        DispositifFinancement.Autofinancement => "Autofinancement",
        DispositifFinancement.Entreprise => "Entreprise",
        DispositifFinancement.Autre => "Autre",
        _ => "Non sécurisé",
    };
}
