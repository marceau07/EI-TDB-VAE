using EiVae.Api.Services;
using EiVae.Domain;
using EiVae.Domain.Entities;
using EiVae.Domain.Services;
using EiVae.Infrastructure;
using EiVae.Infrastructure.Documents;
using EiVae.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EiVae.Api.Endpoints;

/// <summary>Création ou mise à jour d'un dossier candidat depuis l'interface de saisie.</summary>
public sealed record SaisieCandidat(
    string Nom,
    string Prenom,
    string? Email,
    string? Telephone,
    string? Ville,
    string? CodePostal,
    string? Departement,
    string? Region,
    int? CertificationId,
    OrigineCandidature Origine,
    DateOnly? DateDemande,
    int? AapId,
    int? AccompagnateurId,
    EtapeParcours? Etape,
    StatutSecondaire? StatutSecondaire,
    decimal? HeuresIndividuel,
    decimal? HeuresCollectif,
    decimal? HeuresComplementFormatif,
    bool ForfaitArchitecture,
    bool FraisJuryInclus,
    DateOnly? DateDebutParcours,
    string? CodeAcfSolei,
    string? CandidatureFranceVaeId,
    string? Notes,
    bool ConsentementRgpd,
    List<int>? ComplementsFormatifs = null,
    List<int>? ModulesElearning = null);

public sealed record SaisieJalons(
    DateOnly? PremierContact, DateOnly? RecueilBesoins, DateOnly? RdvFaisabilite,
    DateOnly? DepotFaisabilite, DateOnly? ParcoursValide, DateOnly? Recevabilite,
    DateOnly? DebutAccompagnement, DateOnly? DepotDossierValidation, DateOnly? Jury,
    DateOnly? EntretienPostJury, ResultatJury? ResultatJury, MotifSortie? MotifSortie,
    string? CommentaireSortie);

public sealed record SaisieSeance(
    DateOnly Date, NatureHeure Nature, decimal DureeHeures, int? IntervenantId,
    string? Modalite, string? Objet, bool Emargee, bool Realisee);

public sealed record SaisieFinancement(
    DispositifFinancement Dispositif, string? Financeur, string? NumeroPriseEnCharge,
    decimal? MontantAccorde, decimal? ResteACharge, DateOnly? DateDemande, DateOnly? DateSecurisation,
    string? Commentaire);

public sealed record SaisieFacture(
    string Numero, DateOnly DateEmission, string? Financeur, decimal MontantHt,
    DateOnly? DateReglement, string? CodeAcf);

public sealed record SaisieConsentement(bool ConsentementRgpd);

public sealed record SaisieAffectation(ActeurDossier Acteur, int? IntervenantId);

/// <summary>
/// Action corrective saisie depuis une alerte. Seuls les champs utiles à la
/// règle concernée sont lus ; <see cref="ReporterJusquA"/> transforme la
/// résolution en report.
/// </summary>
public sealed record ResolutionAlerte(
    DateOnly? Date = null,
    int? IntervenantId = null,
    string? Commentaire = null,
    DispositifFinancement? Dispositif = null,
    string? Financeur = null,
    string? NumeroPriseEnCharge = null,
    decimal? MontantAccorde = null,
    string? NumeroFacture = null,
    decimal? MontantHt = null,
    decimal? HeuresIndividuel = null,
    decimal? HeuresCollectif = null,
    decimal? HeuresComplementFormatif = null,
    DateOnly? ReporterJusquA = null);

public static class CandidatsEndpoints
{
    public static void MapCandidatsEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/parcours").WithTags("Parcours candidats");

        // ------------------------------------------------------------ lecture
        g.MapGet("/", async (
                [AsParameters] FiltreTableauDeBord filtre,
                ServiceTableauDeBord service,
                CancellationToken ct) =>
            Results.Ok(await service.ListerAsync(filtre, ct)))
            .WithSummary("Liste les dossiers, filtrée par les critères transverses.");

        g.MapGet("/synthese", async (
                [AsParameters] FiltreTableauDeBord filtre,
                ServiceTableauDeBord service,
                CancellationToken ct) =>
            Results.Ok(await service.SyntheseAsync(filtre, ct)))
            .WithSummary("Indicateurs de synthèse du tableau de bord direction.");

        g.MapGet("/alertes", async (
                [AsParameters] FiltreTableauDeBord filtre,
                ServiceTableauDeBord service,
                CancellationToken ct) =>
            Results.Ok(await service.AlertesAsync(filtre, ct)))
            .WithSummary("Plan d'action : les alertes ouvertes, classées par sévérité puis par ancienneté.");

        g.MapGet("/{id:int}", async (
                int id, VaeDbContext db, ParametresService parametres,
                SharePointLinkBuilder sharePoint, GenerateurDossierCandidat generateur, CancellationToken ct) =>
        {
            var p = await db.Parcours
                .Include(x => x.Candidat)
                .Include(x => x.Certification).ThenInclude(c => c!.Blocs)
                .Include(x => x.Certification).ThenInclude(c => c!.Certificateurs).ThenInclude(cc => cc.Certificateur)
                .Include(x => x.Aap)
                .Include(x => x.Accompagnateur)
                .Include(x => x.Gestionnaire)
                .Include(x => x.Financements)
                .Include(x => x.Factures)
                .Include(x => x.Seances).ThenInclude(s => s.Intervenant)
                .Include(x => x.Pieces)
                .Include(x => x.HistoriqueStatuts)
                .Include(x => x.Alertes.Where(a => a.ResolueLe == null))
                .Include(x => x.Cohorte).ThenInclude(c => c!.ProjetCollectif)
                .Include(x => x.Modules).ThenInclude(m => m.ModuleAcademie)
                .AsSplitQuery()
                .FirstOrDefaultAsync(x => x.Id == id, ct);

            if (p is null)
            {
                return Results.NotFound();
            }

            var tarification = await parametres.TarificationAsync(ct);
            var calcul = tarification.Calculer(p);
            var grille = tarification.GrillePour(p);
            var regles = await parametres.ReglesParCodeAsync(ct);

            return Results.Ok(new
            {
                parcours = new
                {
                    p.Id, p.CandidatId,
                    candidat = new
                    {
                        p.Candidat!.Nom, p.Candidat.Prenom, p.Candidat.Email, p.Candidat.Telephone,
                        p.Candidat.Ville, p.Candidat.CodePostal, p.Candidat.Departement, p.Candidat.Region,
                        p.Candidat.ConsentementRgpd, p.Candidat.DateConsentementRgpd,
                        p.Candidat.IdentifiantFranceVae, p.Candidat.Notes,
                    },
                    etape = MoteurAlertes.Libelle(p.Etape),
                    etapeRang = (int)p.Etape,
                    etapeCode = p.Etape.ToString(),
                    statutSecondaire = ServiceTableauDeBord.LibelleStatut(p.StatutSecondaire),
                    statutSecondaireCode = p.StatutSecondaire.ToString(),
                    p.ForfaitArchitectureApplique, p.FraisJuryInclus,
                    motifSortie = ServiceTableauDeBord.LibelleSortie(p.MotifSortie),
                    p.CommentaireSortie,
                    origine = p.Origine.ToString(),
                    aap = p.Aap is null ? null : new { p.Aap.Id, nom = p.Aap.NomComplet },
                    accompagnateur = p.Accompagnateur is null
                        ? null
                        : new { p.Accompagnateur.Id, nom = p.Accompagnateur.NomComplet },
                    gestionnaire = p.Gestionnaire is null
                        ? null
                        : new { p.Gestionnaire.Id, nom = p.Gestionnaire.NomComplet },
                    jalons = new
                    {
                        p.DateDemande, p.DatePremierContact, p.DateRecueilBesoins, p.DateRdvFaisabilite,
                        p.DateDepotFaisabilite, p.DateParcoursValide, p.DateRecevabilite,
                        p.DateDebutAccompagnement, p.DateDepotDossierValidation, p.DateJury,
                        p.DateEntretienPostJury, p.DateDebutParcours, p.DateDernierMouvement,
                    },
                    heures = new
                    {
                        individuelPrescrit = p.HeuresIndividuelPrescrites,
                        collectifPrescrit = p.HeuresCollectifPrescrites,
                        complementPrescrit = p.HeuresComplementFormatifPrescrites,
                        total = p.HeuresPrescritesTotales,
                        realisees = p.Seances.Where(s => s.Realisee && s.Nature != NatureHeure.Asynchrone)
                                             .Sum(s => s.DureeHeures),
                        plafondIndividuel = grille.PlafondHeuresIndividuel,
                        plafondCollectif = grille.PlafondHeuresCollectif,
                        plafondComplement = grille.PlafondHeuresComplementFormatif,
                    },
                    resultatJury = p.ResultatJury.ToString(),
                    p.CodeAcfSolei, p.CandidatureFranceVaeId, p.EspaceAcademieCree, p.UrlEspaceAcademie,
                    p.DateDerniereActiviteAcademie, p.Historique,
                    sharePoint = new
                    {
                        chemin = sharePoint.CheminDossierCandidat(p),
                        url = sharePoint.EstConfigure ? sharePoint.UrlDossierCandidat(p) : null,
                        configure = sharePoint.EstConfigure,
                        dossierLocalConfigure = generateur.EstConfigure,
                    },
                    cohorte = p.Cohorte is null
                        ? null
                        : new
                        {
                            p.Cohorte.Id, p.Cohorte.Nom,
                            projet = p.Cohorte.ProjetCollectif?.Nom,
                            entreprise = p.Cohorte.ProjetCollectif?.RaisonSociale,
                        },
                },
                certification = p.Certification is null
                    ? null
                    : new
                    {
                        p.Certification.Id, p.Certification.CodeRncp, p.Certification.Intitule,
                        p.Certification.Abrege, p.Certification.Niveau, p.Certification.DomaineEi,
                        p.Certification.VoieVaeOuverte, p.Certification.LienFranceCompetences,
                        certificateurs = p.Certification.Certificateurs
                            .Select(c => new { c.Certificateur!.Nom, c.Certificateur.Siret, c.EstPrincipal }),
                        blocs = p.Certification.Blocs.OrderBy(b => b.Ordre)
                            .Select(b => new { b.Code, b.Libelle }),
                        contact = new
                        {
                            p.Certification.ContactCertificateurNom,
                            p.Certification.ContactCertificateurEmail,
                            p.Certification.ContactCertificateurTelephone,
                        },
                    },
                economie = new
                {
                    grille = grille.Code,
                    grilleLibelle = grille.Libelle,
                    grilleEffet = grille.DateEffet,
                    calcul.Forfait, calcul.MontantIndividuel, calcul.MontantCollectif,
                    calcul.MontantComplementFormatif, calcul.FraisJury, calcul.Total,
                    calcul.CoutPedagogique, calcul.Marge, calcul.TauxMarge, calcul.Depassements,
                    tarifs = new
                    {
                        grille.ForfaitArchitecture, grille.TarifHoraireIndividuel,
                        grille.TarifHoraireCollectif, grille.TarifHoraireComplementFormatif, grille.FraisJury,
                    },
                },
                modules = p.Modules.OrderBy(m => m.ModuleAcademie!.Nature).ThenBy(m => m.ModuleAcademie!.Titre)
                    .Select(m => new
                    {
                        id = m.ModuleAcademieId, m.ModuleAcademie!.Code, m.ModuleAcademie.Titre,
                        m.ModuleAcademie.Nature, m.ModuleAcademie.Type, m.ModuleAcademie.DureeHeures,
                        m.ModuleAcademie.Url, m.Statut, m.DateAttribution, m.DateFin,
                    }),
                financements = p.Financements.Select(f => new
                {
                    f.Id, dispositif = f.Dispositif.ToString(),
                    dispositifLibelle = ServiceTableauDeBord.LibelleDispositif(f.Dispositif),
                    f.Financeur, f.NumeroPriseEnCharge, f.MontantAccorde, f.ResteACharge,
                    f.DateDemande, f.DateSecurisation, f.EstSecurise, f.Commentaire,
                }),
                factures = p.Factures.Select(f => new
                {
                    f.Id, f.Numero, f.DateEmission, f.Financeur, f.MontantHt, f.DateReglement, f.CodeAcf,
                }),
                seances = p.Seances.OrderByDescending(s => s.Date).Select(s => new
                {
                    s.Id, s.Date, nature = s.Nature.ToString(), s.DureeHeures, s.Modalite,
                    s.Objet, s.Emargee, s.Realisee,
                    intervenant = s.Intervenant?.NomComplet,
                }),
                pieces = p.Pieces.Select(x => new
                {
                    x.Id, x.Type, x.Libelle, x.Obligatoire, x.Presente, x.DateDepot,
                    exigibleAPartirDe = MoteurAlertes.Libelle(x.ExigibleAPartirDe),
                    url = sharePoint.EstConfigure ? sharePoint.UrlPiece(p, x) : null,
                }),
                alertes = p.Alertes.Where(a => a.EstOuverte).Select(a =>
                {
                    var regle = regles.GetValueOrDefault(a.CodeRegle);
                    var attendu = MoteurAlertes.Jalon(regle?.JalonAttendu);
                    var reference = MoteurAlertes.Jalon(regle?.JalonReference);
                    return new
                    {
                        a.Id, a.CodeRegle, severite = a.Severite.ToString().ToLowerInvariant(),
                        libelle = regle?.Libelle ?? "Règle supprimée",
                        actionAttendue = regle?.ActionAttendue ?? "—",
                        regle?.Responsable, regle?.Fondement,
                        a.Detail, a.DetecteeLe, a.ReporteeJusquA, a.MotifReport,
                        condition = regle?.Condition,
                        acteur = regle?.Acteur,
                        jalonAttendu = attendu is null ? null : new { attendu.Code, attendu.Libelle },
                        jalonReference = reference is null
                            ? null
                            : new { reference.Code, reference.Libelle, date = reference.Lire(p) },
                        montantAFacturer = regle?.Condition == TypeCondition.FactureManquante ? calcul.Total : (decimal?)null,
                    };
                }),
                historique = p.HistoriqueStatuts.OrderByDescending(h => h.SurvenuLe).Take(30).Select(h => new
                {
                    h.SurvenuLe, h.Auteur, h.Commentaire,
                    de = h.EtapePrecedente is null ? null : MoteurAlertes.Libelle(h.EtapePrecedente.Value),
                    vers = MoteurAlertes.Libelle(h.EtapeNouvelle),
                }),
            });
        }).WithSummary("Fiche complète d'un dossier : la source de toutes les données du tableau de bord.");

        // ------------------------------------------------------------ création
        g.MapPost("/", async (
                SaisieCandidat saisie, VaeDbContext db, ParametresService parametres,
                ServiceAlertes alertes, ServiceNotifications notifications,
                GenerateurDossierCandidat generateur, ILoggerFactory journaux, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(saisie.Nom) || string.IsNullOrWhiteSpace(saisie.Prenom))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["nom"] = ["Le nom et le prénom sont obligatoires."],
                });
            }

            // Un doublon sur le même couple nom/prénom et la même certification est
            // presque toujours une double saisie : on le signale sans bloquer,
            // car les homonymes existent.
            var doublon = await db.Parcours.Include(p => p.Candidat).FirstOrDefaultAsync(
                p => p.Candidat!.Nom.ToUpper() == saisie.Nom.ToUpper()
                     && p.Candidat.Prenom.ToUpper() == saisie.Prenom.ToUpper()
                     && p.CertificationId == saisie.CertificationId, ct);

            var candidat = new Candidat
            {
                Nom = saisie.Nom.Trim().ToUpperInvariant(),
                Prenom = Capitaliser(saisie.Prenom.Trim()),
                Email = Vide(saisie.Email),
                Telephone = Vide(saisie.Telephone),
                Ville = Vide(saisie.Ville),
                CodePostal = Vide(saisie.CodePostal),
                Departement = Vide(saisie.Departement),
                Region = Vide(saisie.Region),
                Notes = Vide(saisie.Notes),
            };
            DefinirConsentement(candidat, saisie.ConsentementRgpd);

            var aujourdHui = DateOnly.FromDateTime(DateTime.UtcNow);

            var parcours = new Parcours
            {
                Candidat = candidat,
                CertificationId = saisie.CertificationId,
                Origine = saisie.Origine,
                Etape = saisie.Etape ?? EtapeParcours.DemandeRecue,
                StatutSecondaire = saisie.StatutSecondaire ?? StatutSecondaire.Aucun,
                AapId = saisie.AapId,
                AccompagnateurId = saisie.AccompagnateurId,
                DateDemande = saisie.DateDemande ?? aujourdHui,
                DateDebutParcours = saisie.DateDebutParcours,
                HeuresIndividuelPrescrites = saisie.HeuresIndividuel ?? 0,
                HeuresCollectifPrescrites = saisie.HeuresCollectif ?? 0,
                HeuresComplementFormatifPrescrites = saisie.HeuresComplementFormatif ?? 0,
                ForfaitArchitectureApplique = saisie.ForfaitArchitecture,
                FraisJuryInclus = saisie.FraisJuryInclus,
                CodeAcfSolei = Vide(saisie.CodeAcfSolei),
                CandidatureFranceVaeId = Vide(saisie.CandidatureFranceVaeId),
                DateDernierMouvement = aujourdHui,
            };

            // La grille est figée dès la création : c'est elle qui servira au devis.
            var tarification = await parametres.TarificationAsync(ct);
            parcours.DateDebutParcours ??= parcours.CalculerDateDebut() ?? aujourdHui;
            parcours.GrilleTarifaireId = tarification.GrillePour(parcours.DateDebutParcours.Value).Id;

            parcours.HistoriqueStatuts.Add(new HistoriqueStatut
            {
                EtapeNouvelle = parcours.Etape,
                StatutSecondaire = parcours.StatutSecondaire,
                Commentaire = "Création du dossier",
            });

            db.Candidats.Add(candidat);
            db.Parcours.Add(parcours);
            await SynchroniserModulesAsync(db, parcours, NatureModule.ComplementFormatif, saisie.ComplementsFormatifs, ct);
            await SynchroniserModulesAsync(db, parcours, NatureModule.ELearning, saisie.ModulesElearning, ct);
            await db.SaveChangesAsync(ct);
            await alertes.RafraichirAsync(parcours.Id, ct);
            var dossier = await GenererDossierAsync(generateur, parcours.Id, journaux, ct);
            await notifications.NotifierAsync(parcours.Id, null, ct);

            return Results.Created($"/api/parcours/{parcours.Id}", new
            {
                parcours.Id,
                doublonPossible = doublon is not null,
                doublonId = doublon?.Id,
                grille = (await parametres.TarificationAsync(ct)).GrillePour(parcours).Code,
                dossier,
            });
        }).WithSummary("Crée un dossier candidat depuis la saisie manuelle.");

        // ------------------------------------------------------------ mise à jour
        g.MapPut("/{id:int}", async (
                int id, SaisieCandidat saisie, VaeDbContext db, ParametresService parametres,
                ServiceAlertes alertes, ServiceNotifications notifications, GenerateurDossierCandidat generateur, CancellationToken ct) =>
        {
            var p = await db.Parcours.Include(x => x.Candidat)
                .Include(x => x.Modules).ThenInclude(m => m.ModuleAcademie)
                .FirstOrDefaultAsync(x => x.Id == id, ct);
            if (p is null)
            {
                return Results.NotFound();
            }

            var etapePrecedente = p.Etape;
            var avant = EtatDossier.De(p);

            p.Candidat!.Nom = saisie.Nom.Trim().ToUpperInvariant();
            p.Candidat.Prenom = Capitaliser(saisie.Prenom.Trim());
            p.Candidat.Email = Vide(saisie.Email);
            p.Candidat.Telephone = Vide(saisie.Telephone);
            p.Candidat.Ville = Vide(saisie.Ville);
            p.Candidat.CodePostal = Vide(saisie.CodePostal);
            p.Candidat.Departement = Vide(saisie.Departement);
            p.Candidat.Region = Vide(saisie.Region);
            p.Candidat.Notes = Vide(saisie.Notes);
            DefinirConsentement(p.Candidat, saisie.ConsentementRgpd);

            p.CertificationId = saisie.CertificationId;
            p.Origine = saisie.Origine;
            p.AapId = saisie.AapId;
            p.AccompagnateurId = saisie.AccompagnateurId;
            p.DateDemande = saisie.DateDemande ?? p.DateDemande;
            p.HeuresIndividuelPrescrites = saisie.HeuresIndividuel ?? 0;
            p.HeuresCollectifPrescrites = saisie.HeuresCollectif ?? 0;
            p.HeuresComplementFormatifPrescrites = saisie.HeuresComplementFormatif ?? 0;
            p.ForfaitArchitectureApplique = saisie.ForfaitArchitecture;
            p.FraisJuryInclus = saisie.FraisJuryInclus;
            p.CodeAcfSolei = Vide(saisie.CodeAcfSolei);
            p.CandidatureFranceVaeId = Vide(saisie.CandidatureFranceVaeId);
            p.DateDernierMouvement = DateOnly.FromDateTime(DateTime.UtcNow);

            if (saisie.Etape is { } etape)
            {
                p.Etape = etape;
            }

            await SynchroniserModulesAsync(db, p, NatureModule.ComplementFormatif, saisie.ComplementsFormatifs, ct);
            await SynchroniserModulesAsync(db, p, NatureModule.ELearning, saisie.ModulesElearning, ct);

            if (saisie.StatutSecondaire is { } statut)
            {
                p.StatutSecondaire = statut;
            }

            // Déplacer la date de démarrage change potentiellement la grille : on
            // la recalcule explicitement, et on le signale dans la réponse.
            var ancienneGrille = p.GrilleTarifaireId;
            if (saisie.DateDebutParcours is { } debut && debut != p.DateDebutParcours)
            {
                p.DateDebutParcours = debut;
                var tarification = await parametres.TarificationAsync(ct);
                p.GrilleTarifaireId = tarification.GrillePour(debut).Id;
            }

            if (etapePrecedente != p.Etape)
            {
                db.HistoriqueStatuts.Add(new HistoriqueStatut
                {
                    ParcoursId = p.Id,
                    EtapePrecedente = etapePrecedente,
                    EtapeNouvelle = p.Etape,
                    StatutSecondaire = p.StatutSecondaire,
                    Commentaire = "Changement d'étape",
                });
            }

            await db.SaveChangesAsync(ct);
            await alertes.RafraichirAsync(p.Id, ct);
            await generateur.MettreAJourAsync(p.Id, ct);
            await notifications.NotifierAsync(p.Id, avant, ct);

            return Results.Ok(new
            {
                p.Id,
                grilleChangee = ancienneGrille != p.GrilleTarifaireId,
                grille = (await parametres.TarificationAsync(ct)).GrillePour(p).Code,
            });
        }).WithSummary("Met à jour un dossier. Recalcule la grille si la date de démarrage change.");

        g.MapPut("/{id:int}/jalons", async (
                int id, SaisieJalons j, VaeDbContext db, ServiceAlertes alertes,
                ServiceNotifications notifications, GenerateurDossierCandidat generateur, CancellationToken ct) =>
        {
            var p = await db.Parcours.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (p is null)
            {
                return Results.NotFound();
            }

            var avant = EtatDossier.De(p);

            p.DatePremierContact = j.PremierContact ?? p.DatePremierContact;
            p.DateRecueilBesoins = j.RecueilBesoins ?? p.DateRecueilBesoins;
            p.DateRdvFaisabilite = j.RdvFaisabilite ?? p.DateRdvFaisabilite;
            p.DateDepotFaisabilite = j.DepotFaisabilite ?? p.DateDepotFaisabilite;
            p.DateParcoursValide = j.ParcoursValide ?? p.DateParcoursValide;
            p.DateRecevabilite = j.Recevabilite ?? p.DateRecevabilite;
            p.DateDebutAccompagnement = j.DebutAccompagnement ?? p.DateDebutAccompagnement;
            p.DateDepotDossierValidation = j.DepotDossierValidation ?? p.DateDepotDossierValidation;
            p.DateJury = j.Jury ?? p.DateJury;
            p.DateEntretienPostJury = j.EntretienPostJury ?? p.DateEntretienPostJury;
            p.DateDernierMouvement = DateOnly.FromDateTime(DateTime.UtcNow);

            if (j.ResultatJury is { } r)
            {
                p.ResultatJury = r;
            }

            if (j.MotifSortie is { } m && m != MotifSortie.Aucun)
            {
                p.MotifSortie = m;
                p.Etape = EtapeParcours.Sortie;
                p.CommentaireSortie = j.CommentaireSortie;
            }

            await db.SaveChangesAsync(ct);
            await alertes.RafraichirAsync(p.Id, ct);
            await generateur.MettreAJourAsync(p.Id, ct);
            await notifications.NotifierAsync(p.Id, avant, ct);
            return Results.Ok(new { p.Id });
        }).WithSummary("Enregistre les dates de jalons et le résultat de jury.");

        // ------------------------------------------------------------ consentement et affectation
        g.MapPut("/{id:int}/consentement", async (
                int id, SaisieConsentement s, VaeDbContext db, ServiceAlertes alertes,
                GenerateurDossierCandidat generateur, CancellationToken ct) =>
        {
            var p = await db.Parcours.Include(x => x.Candidat).FirstOrDefaultAsync(x => x.Id == id, ct);
            if (p is null)
            {
                return Results.NotFound();
            }

            DefinirConsentement(p.Candidat!, s.ConsentementRgpd);
            await db.SaveChangesAsync(ct);
            await alertes.RafraichirAsync(p.Id, ct);
            await generateur.MettreAJourAsync(p.Id, ct);
            return Results.Ok(new { p.Candidat!.ConsentementRgpd, p.Candidat.DateConsentementRgpd });
        }).WithSummary("Enregistre ou retire le consentement RGPD du candidat.");

        g.MapPut("/{id:int}/affectation", async (
                int id, SaisieAffectation s, VaeDbContext db, ServiceAlertes alertes,
                ServiceNotifications notifications, GenerateurDossierCandidat generateur, CancellationToken ct) =>
        {
            var p = await db.Parcours.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (p is null)
            {
                return Results.NotFound();
            }

            if (s.IntervenantId is { } iid && !await db.Intervenants.AnyAsync(i => i.Id == iid, ct))
            {
                return Results.NotFound(new { message = "Intervenant introuvable." });
            }

            var avant = EtatDossier.De(p);
            Affecter(p, s.Acteur, s.IntervenantId);
            p.DateDernierMouvement = DateOnly.FromDateTime(DateTime.UtcNow);

            await db.SaveChangesAsync(ct);
            await alertes.RafraichirAsync(p.Id, ct);
            await generateur.MettreAJourAsync(p.Id, ct);
            var envoyees = await notifications.NotifierAsync(p.Id, avant, ct);
            return Results.Ok(new { p.Id, notifications = envoyees });
        }).WithSummary("Affecte un AAP, un accompagnateur ou un gestionnaire, et notifie l'intéressé.");

        // ------------------------------------------------------------ dossier documentaire
        g.MapPost("/{id:int}/dossier", async (
                int id, VaeDbContext db, GenerateurDossierCandidat generateur, CancellationToken ct) =>
        {
            if (!await db.Parcours.AnyAsync(x => x.Id == id, ct))
            {
                return Results.NotFound();
            }

            return Results.Ok(await generateur.GenererAsync(id, ct));
        }).WithSummary("Crée le dossier du candidat s'il n'existe pas et régénère sa fiche Word.");

        g.MapGet("/{id:int}/fiche.docx", async (int id, GenerateurDossierCandidat generateur, CancellationToken ct) =>
            await generateur.FicheAsync(id, ct) is { } fiche
                ? Results.File(fiche.Contenu,
                    "application/vnd.openxmlformats-officedocument.wordprocessingml.document", fiche.NomFichier)
                : Results.NotFound())
            .WithSummary("Télécharge la fiche Word du candidat.");

        // ------------------------------------------------------------ séances
        g.MapPost("/{id:int}/seances", async (
                int id, SaisieSeance s, VaeDbContext db, ServiceAlertes alertes,
                GenerateurDossierCandidat generateur, CancellationToken ct) =>
        {
            if (!await db.Parcours.AnyAsync(x => x.Id == id, ct))
            {
                return Results.NotFound();
            }

            if (s.DureeHeures <= 0)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["dureeHeures"] = ["La durée doit être supérieure à zéro."],
                });
            }

            var seance = new Seance
            {
                ParcoursId = id, Date = s.Date, Nature = s.Nature, DureeHeures = s.DureeHeures,
                IntervenantId = s.IntervenantId, Modalite = s.Modalite, Objet = s.Objet,
                Emargee = s.Emargee, Realisee = s.Realisee,
            };

            db.Seances.Add(seance);

            var p = await db.Parcours.FirstAsync(x => x.Id == id, ct);
            p.DateDernierMouvement = DateOnly.FromDateTime(DateTime.UtcNow);

            await db.SaveChangesAsync(ct);
            await alertes.RafraichirAsync(id, ct);
            await generateur.MettreAJourAsync(id, ct);
            return Results.Created($"/api/parcours/{id}/seances/{seance.Id}", new { seance.Id });
        }).WithSummary("Enregistre une séance réalisée : c'est la pièce qui sécurise la cohérence devis / prestations.");

        g.MapDelete("/{id:int}/seances/{seanceId:int}", async (
                int id, int seanceId, VaeDbContext db, ServiceAlertes alertes,
                GenerateurDossierCandidat generateur, CancellationToken ct) =>
        {
            var s = await db.Seances.FirstOrDefaultAsync(x => x.Id == seanceId && x.ParcoursId == id, ct);
            if (s is null)
            {
                return Results.NotFound();
            }

            db.Seances.Remove(s);
            await db.SaveChangesAsync(ct);
            await alertes.RafraichirAsync(id, ct);
            await generateur.MettreAJourAsync(id, ct);
            return Results.NoContent();
        });

        // ------------------------------------------------------------ financement et facturation
        g.MapPost("/{id:int}/financements", async (
                int id, SaisieFinancement f, VaeDbContext db, ServiceAlertes alertes,
                GenerateurDossierCandidat generateur, CancellationToken ct) =>
        {
            if (!await db.Parcours.AnyAsync(x => x.Id == id, ct))
            {
                return Results.NotFound();
            }

            var financement = new Financement
            {
                ParcoursId = id, Dispositif = f.Dispositif, Financeur = f.Financeur,
                NumeroPriseEnCharge = f.NumeroPriseEnCharge, MontantAccorde = f.MontantAccorde,
                ResteACharge = f.ResteACharge, DateDemande = f.DateDemande,
                DateSecurisation = f.DateSecurisation, Commentaire = f.Commentaire,
            };

            db.Financements.Add(financement);
            await db.SaveChangesAsync(ct);
            await alertes.RafraichirAsync(id, ct);
            await generateur.MettreAJourAsync(id, ct);
            return Results.Created($"/api/parcours/{id}/financements/{financement.Id}", new { financement.Id });
        }).WithSummary("Ajoute un dispositif de financement au dossier.");

        g.MapPut("/{id:int}/financements/{financementId:int}", async (
                int id, int financementId, SaisieFinancement f, VaeDbContext db, ServiceAlertes alertes,
                GenerateurDossierCandidat generateur, CancellationToken ct) =>
        {
            var financement = await db.Financements
                .FirstOrDefaultAsync(x => x.Id == financementId && x.ParcoursId == id, ct);
            if (financement is null)
            {
                return Results.NotFound();
            }

            financement.Dispositif = f.Dispositif;
            financement.Financeur = Vide(f.Financeur);
            financement.NumeroPriseEnCharge = Vide(f.NumeroPriseEnCharge);
            financement.MontantAccorde = f.MontantAccorde;
            financement.ResteACharge = f.ResteACharge;
            financement.DateDemande = f.DateDemande;
            financement.DateSecurisation = f.DateSecurisation;
            financement.Commentaire = Vide(f.Commentaire);

            var p = await db.Parcours.FirstAsync(x => x.Id == id, ct);
            p.DateDernierMouvement = DateOnly.FromDateTime(DateTime.UtcNow);

            await db.SaveChangesAsync(ct);
            await alertes.RafraichirAsync(id, ct);
            await generateur.MettreAJourAsync(id, ct);
            return Results.Ok(new { financement.Id });
        }).WithSummary("Modifie un financement du dossier.");

        g.MapDelete("/{id:int}/financements/{financementId:int}", async (
                int id, int financementId, VaeDbContext db, ServiceAlertes alertes,
                GenerateurDossierCandidat generateur, CancellationToken ct) =>
        {
            var financement = await db.Financements
                .FirstOrDefaultAsync(x => x.Id == financementId && x.ParcoursId == id, ct);
            if (financement is null)
            {
                return Results.NotFound();
            }

            db.Financements.Remove(financement);
            await db.SaveChangesAsync(ct);
            await alertes.RafraichirAsync(id, ct);
            await generateur.MettreAJourAsync(id, ct);
            return Results.NoContent();
        }).WithSummary("Supprime un financement saisi par erreur.");

        g.MapPost("/{id:int}/factures", async (
                int id, SaisieFacture f, VaeDbContext db, ServiceAlertes alertes,
                GenerateurDossierCandidat generateur, CancellationToken ct) =>
        {
            if (!await db.Parcours.AnyAsync(x => x.Id == id, ct))
            {
                return Results.NotFound();
            }

            if (await db.Factures.AnyAsync(x => x.Numero == f.Numero, ct))
            {
                return Results.Conflict(new { message = $"La facture {f.Numero} existe déjà." });
            }

            var facture = new Facture
            {
                ParcoursId = id, Numero = f.Numero, DateEmission = f.DateEmission,
                Financeur = f.Financeur, MontantHt = f.MontantHt,
                DateReglement = f.DateReglement, CodeAcf = f.CodeAcf,
            };

            db.Factures.Add(facture);
            await db.SaveChangesAsync(ct);
            await alertes.RafraichirAsync(id, ct);
            await generateur.MettreAJourAsync(id, ct);
            return Results.Created($"/api/parcours/{id}/factures/{facture.Id}", new { facture.Id });
        }).WithSummary("Enregistre une facture émise sur le dossier.");

        // ------------------------------------------------------------ alertes
        g.MapPost("/alertes/{alerteId:long}/reporter", async (
                long alerteId, [FromBody] ReportAlerte report, ServiceAlertes service, CancellationToken ct) =>
            await service.ReporterAsync(alerteId, report.JusquA, report.Motif, ct)
                ? Results.NoContent()
                : Results.NotFound())
            .WithSummary("Reporte une alerte : elle reste vraie mais sort du plan d'action.");

        g.MapPost("/alertes/{alerteId:long}/resoudre", async (
                long alerteId, ResolutionAlerte r, VaeDbContext db, ParametresService parametres,
                ServiceAlertes service, ServiceNotifications notifications, GenerateurDossierCandidat generateur, CancellationToken ct) =>
        {
            var alerte = await db.Alertes.FirstOrDefaultAsync(a => a.Id == alerteId, ct);
            if (alerte is null)
            {
                return Results.NotFound();
            }

            var aujourdHui = DateOnly.FromDateTime(DateTime.UtcNow);

            if (r.ReporterJusquA is { } report)
            {
                alerte.ReporteeJusquA = report;
                alerte.MotifReport = Vide(r.Commentaire);
                await db.SaveChangesAsync(ct);
                return Results.Ok(new { resolue = false, reportee = true, message = $"Alerte reportée au {report:dd/MM/yyyy}." });
            }

            var regle = (await parametres.ReglesParCodeAsync(ct)).GetValueOrDefault(alerte.CodeRegle);
            if (regle is null)
            {
                // Règle supprimée depuis la détection : il n'y a plus rien à corriger.
                alerte.ResolueLe = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync(ct);
                return Results.Ok(new { resolue = true, message = "Alerte close : sa règle n'existe plus." });
            }

            var p = await db.Parcours
                .Include(x => x.Candidat)
                .Include(x => x.Financements)
                .Include(x => x.Factures)
                .FirstAsync(x => x.Id == alerte.ParcoursId, ct);

            var avant = EtatDossier.De(p);
            string? erreur = null;
            var action = string.Empty;

            switch (regle.Condition)
            {
                case TypeCondition.DelaiDepuisJalon when MoteurAlertes.Jalon(regle.JalonAttendu) is { } attendu:
                    DefinirJalon(p, attendu.Code, r.Date ?? aujourdHui);
                    action = $"{attendu.Libelle} au {r.Date ?? aujourdHui:dd/MM/yyyy}";
                    break;

                case TypeCondition.DelaiDepuisJalon when regle.JalonReference == nameof(Parcours.DateDernierMouvement):
                    action = "contact consigné";
                    break;

                case TypeCondition.EcheanceProche:
                    alerte.ReporteeJusquA = MoteurAlertes.Jalon(regle.JalonReference)?.Lire(p) ?? aujourdHui.AddDays(7);
                    alerte.MotifReport = Vide(r.Commentaire) ?? "Préparation engagée";
                    action = "préparation engagée";
                    break;

                case TypeCondition.FinancementNonSecurise:
                    var enAttente = p.Financements.FirstOrDefault(f => !f.EstSecurise);
                    if (r.Dispositif is null && enAttente is not null)
                    {
                        enAttente.DateSecurisation = r.Date ?? aujourdHui;
                        enAttente.NumeroPriseEnCharge ??= Vide(r.NumeroPriseEnCharge);
                        action = $"financement {ServiceTableauDeBord.LibelleDispositif(enAttente.Dispositif)} sécurisé";
                    }
                    else if (r.Dispositif is { } dispositif)
                    {
                        db.Financements.Add(new Financement
                        {
                            ParcoursId = p.Id, Dispositif = dispositif, Financeur = Vide(r.Financeur),
                            NumeroPriseEnCharge = Vide(r.NumeroPriseEnCharge), MontantAccorde = r.MontantAccorde,
                            DateDemande = r.Date ?? aujourdHui, DateSecurisation = r.Date ?? aujourdHui,
                        });
                        action = $"financement {ServiceTableauDeBord.LibelleDispositif(dispositif)} sécurisé";
                    }
                    else
                    {
                        erreur = "Indiquez le dispositif de financement.";
                    }

                    break;

                case TypeCondition.FactureManquante:
                    if (string.IsNullOrWhiteSpace(r.NumeroFacture) || r.MontantHt is not > 0)
                    {
                        erreur = "Le numéro et le montant de la facture sont obligatoires.";
                    }
                    else if (await db.Factures.AnyAsync(f => f.Numero == r.NumeroFacture.Trim(), ct))
                    {
                        return Results.Conflict(new { message = $"La facture {r.NumeroFacture.Trim()} existe déjà." });
                    }
                    else
                    {
                        db.Factures.Add(new Facture
                        {
                            ParcoursId = p.Id, Numero = r.NumeroFacture.Trim(), DateEmission = r.Date ?? aujourdHui,
                            MontantHt = r.MontantHt.Value, Financeur = Vide(r.Financeur),
                        });
                        action = $"facture {r.NumeroFacture.Trim()} émise";
                    }

                    break;

                case TypeCondition.ActeurManquant when regle.Acteur is { } acteur:
                    if (r.IntervenantId is not { } intervenantId
                        || !await db.Intervenants.AnyAsync(i => i.Id == intervenantId, ct))
                    {
                        erreur = "Choisissez l'intervenant à affecter.";
                    }
                    else
                    {
                        Affecter(p, acteur, intervenantId);
                        action = "intervenant affecté";
                    }

                    break;

                case TypeCondition.ConsommationHeures
                    when r.HeuresIndividuel is not null || r.HeuresCollectif is not null
                         || r.HeuresComplementFormatif is not null:
                    p.HeuresIndividuelPrescrites = r.HeuresIndividuel ?? p.HeuresIndividuelPrescrites;
                    p.HeuresCollectifPrescrites = r.HeuresCollectif ?? p.HeuresCollectifPrescrites;
                    p.HeuresComplementFormatifPrescrites = r.HeuresComplementFormatif ?? p.HeuresComplementFormatifPrescrites;
                    action = $"enveloppe portée à {p.HeuresPrescritesTotales:0.#} h";
                    break;

                case TypeCondition.StatutSecondaire:
                    p.StatutSecondaire = StatutSecondaire.Aucun;
                    action = "statut d'attente levé";
                    break;

                case TypeCondition.ConsentementManquant:
                    DefinirConsentement(p.Candidat!, true);
                    action = "consentement RGPD recueilli";
                    break;

                default:
                    erreur = "Cette alerte se lève en faisant avancer le dossier : reportez-la si l'échéance est connue.";
                    break;
            }

            if (erreur is not null)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["resolution"] = [erreur] });
            }

            // Toute action corrective est un mouvement du dossier.
            p.DateDernierMouvement = aujourdHui;
            db.JournalAudit.Add(new JournalAudit
            {
                Entite = "Parcours",
                EntiteId = p.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Action = "Résolution d'alerte",
                Champ = regle.Code,
                NouvelleValeur = string.IsNullOrWhiteSpace(r.Commentaire) ? action : $"{action} — {r.Commentaire.Trim()}",
            });

            await db.SaveChangesAsync(ct);
            await service.RafraichirAsync(p.Id, ct);
            await generateur.MettreAJourAsync(p.Id, ct);
            await notifications.NotifierAsync(p.Id, avant, ct);

            var encoreOuverte = await db.Alertes.AnyAsync(a =>
                a.ParcoursId == p.Id && a.CodeRegle == regle.Code && a.ResolueLe == null
                && (a.ReporteeJusquA == null || a.ReporteeJusquA <= aujourdHui), ct);

            return Results.Ok(new
            {
                resolue = !encoreOuverte,
                message = encoreOuverte
                    ? $"Action enregistrée ({action}), mais la règle {regle.Code} reste vérifiée."
                    : $"Alerte {regle.Code} levée : {action}.",
            });
        }).WithSummary("Applique l'action corrective d'une alerte, puis recalcule les alertes du dossier.");

        g.MapPost("/alertes/rafraichir", async (ServiceAlertes service, CancellationToken ct) =>
            Results.Ok(new { alertesOuvertes = await service.RafraichirAsync(ct) }))
            .WithSummary("Force un recalcul complet du moteur d'alertes.");
    }

    private static string? Vide(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();

    /// <summary>
    /// Aligne les modules d'une nature sur la sélection saisie. Null : la saisie
    /// ne porte pas sur cette nature, rien ne change. Un module conservé garde
    /// son avancement ; seul un module retiré de la sélection disparaît.
    /// </summary>
    private static async Task SynchroniserModulesAsync(
        VaeDbContext db, Parcours p, NatureModule nature, List<int>? selection, CancellationToken ct)
    {
        if (selection is null)
        {
            return;
        }

        var valides = await db.ModulesAcademie
            .Where(m => m.Nature == nature && selection.Contains(m.Id))
            .Select(m => m.Id).ToListAsync(ct);

        foreach (var retire in p.Modules
                     .Where(m => m.ModuleAcademie?.Nature == nature && !valides.Contains(m.ModuleAcademieId))
                     .ToList())
        {
            p.Modules.Remove(retire);
        }

        foreach (var ajoute in valides.Where(v => p.Modules.All(m => m.ModuleAcademieId != v)))
        {
            p.Modules.Add(new ParcoursModule { ModuleAcademieId = ajoute });
        }
    }

    /// <summary>La date de consentement suit la case : posée au premier accord, effacée au retrait.</summary>
    private static void DefinirConsentement(Candidat c, bool consentement)
    {
        if (consentement && !c.ConsentementRgpd)
        {
            c.DateConsentementRgpd = DateOnly.FromDateTime(DateTime.UtcNow);
        }
        else if (!consentement)
        {
            c.DateConsentementRgpd = null;
        }

        c.ConsentementRgpd = consentement;
    }

    private static void Affecter(Parcours p, ActeurDossier acteur, int? intervenantId)
    {
        switch (acteur)
        {
            case ActeurDossier.Aap: p.AapId = intervenantId; break;
            case ActeurDossier.Accompagnateur: p.AccompagnateurId = intervenantId; break;
            case ActeurDossier.Gestionnaire: p.GestionnaireId = intervenantId; break;
        }
    }

    private static void DefinirJalon(Parcours p, string code, DateOnly date)
    {
        switch (code)
        {
            case nameof(Parcours.DateDemande): p.DateDemande = date; break;
            case nameof(Parcours.DatePremierContact): p.DatePremierContact = date; break;
            case nameof(Parcours.DateRecueilBesoins): p.DateRecueilBesoins = date; break;
            case nameof(Parcours.DateRdvFaisabilite): p.DateRdvFaisabilite = date; break;
            case nameof(Parcours.DateDepotFaisabilite): p.DateDepotFaisabilite = date; break;
            case nameof(Parcours.DateRecevabilite): p.DateRecevabilite = date; break;
            case nameof(Parcours.DateParcoursValide): p.DateParcoursValide = date; break;
            case nameof(Parcours.DateDebutAccompagnement): p.DateDebutAccompagnement = date; break;
            case nameof(Parcours.DateDepotDossierValidation): p.DateDepotDossierValidation = date; break;
            case nameof(Parcours.DateJury): p.DateJury = date; break;
            case nameof(Parcours.DateEntretienPostJury): p.DateEntretienPostJury = date; break;
            case nameof(Parcours.DateDebutParcours): p.DateDebutParcours = date; break;
            case nameof(Parcours.DateDernierMouvement): p.DateDernierMouvement = date; break;
        }
    }

    /// <summary>
    /// La création d'un dossier ne doit jamais échouer faute de pouvoir écrire
    /// sur le disque : l'incident est rendu comme un avertissement.
    /// </summary>
    internal static async Task<ResultatDossier?> GenererDossierAsync(
        GenerateurDossierCandidat generateur, int parcoursId, ILoggerFactory journaux, CancellationToken ct)
    {
        try
        {
            return await generateur.GenererAsync(parcoursId, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            journaux.CreateLogger("DossierCandidat").LogError(ex, "Génération du dossier {Parcours} impossible", parcoursId);
            return new ResultatDossier(string.Empty, null, string.Empty, false,
                "La génération du dossier a échoué : " + ex.Message);
        }
    }

    private static string Capitaliser(string v) =>
        v.Length == 0 ? v : char.ToUpperInvariant(v[0]) + v[1..];
}

public sealed record ReportAlerte(DateOnly JusquA, string? Motif);
