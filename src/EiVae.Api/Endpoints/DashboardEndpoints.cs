using EiVae.Api.Services;
using EiVae.Domain;
using EiVae.Domain.Entities;
using EiVae.Domain.Services;
using EiVae.Infrastructure;
using EiVae.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace EiVae.Api.Endpoints;

public static class DashboardEndpoints
{
    /// <summary>Jalons mesurés, avec le délai cible opposable à chaque transition.</summary>
    private static readonly (string Cle, string Libelle, string Rang, int? Cible)[] Jalons =
    [
        ("demande", "Demande reçue", "E1", null),
        ("contact", "Premier contact", "E2", 3),
        ("recueil", "RDV pédagogique", "E3", 11),
        ("faisabilite", "Dépôt faisabilité", "E5", 30),
        ("recevabilite", "Recevabilité", "E6", 60),
        ("valide", "Parcours validé", "E7", null),
        ("depot", "Dépôt dossier de validation", "E9", 180),
        ("jury", "Passage en jury", "E10", 90),
        ("postjury", "Entretien post-jury", "E11", 30),
    ];

    public static void MapDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/pilotage").WithTags("Pilotage");

        // -------------------------------------------------------------- délais et flux
        g.MapGet("/delais", async (
                [AsParameters] FiltreTableauDeBord filtre, ServiceTableauDeBord service, CancellationToken ct) =>
        {
            var parcours = await service.Requete(filtre).AsSplitQuery().ToListAsync(ct);

            DateOnly? Jalon(Parcours p, string cle) => cle switch
            {
                "demande" => p.DateDemande,
                "contact" => p.DatePremierContact,
                "recueil" => p.DateRecueilBesoins,
                "faisabilite" => p.DateDepotFaisabilite,
                "recevabilite" => p.DateRecevabilite,
                "valide" => p.DateParcoursValide,
                "depot" => p.DateDepotDossierValidation,
                "jury" => p.DateJury,
                "postjury" => p.DateEntretienPostJury,
                _ => null,
            };

            // Un dossier compte comme ayant atteint le jalon N s'il a atteint N ou
            // un jalon ultérieur : la saisie laisse des jalons intermédiaires vides,
            // et un entonnoir non monotone produirait des taux de passage aberrants.
            var entonnoir = Jalons.Select((j, i) => new
            {
                j.Cle, j.Libelle, j.Rang,
                Nombre = parcours.Count(p => Jalons.Skip(i).Any(k => Jalon(p, k.Cle) is not null)),
            }).ToList();

            var transitions = new List<object>();
            for (var i = 1; i < Jalons.Length; i++)
            {
                var de = Jalons[i - 1];
                var vers = Jalons[i];

                var ecarts = parcours
                    .Select(p => (Debut: Jalon(p, de.Cle), Fin: Jalon(p, vers.Cle)))
                    .Where(x => x.Debut is not null && x.Fin is not null)
                    .Select(x => x.Fin!.Value.DayNumber - x.Debut!.Value.DayNumber)
                    .Where(j => j is >= 0 and < 1200)
                    .OrderBy(j => j)
                    .ToList();

                if (ecarts.Count == 0)
                {
                    continue;
                }

                transitions.Add(new
                {
                    libelle = vers.Libelle,
                    mesures = ecarts.Count,
                    moyenne = (int)ecarts.Average(),
                    mediane = ecarts[ecarts.Count / 2],
                    cible = vers.Cible,
                    horsDelai = vers.Cible is { } c ? ecarts.Count(j => j > c) : 0,
                });
            }

            // Dernier jalon atteint avant une sortie : c'est là que les dossiers se perdent.
            var pertes = parcours.Where(p => p.Etape == EtapeParcours.Sortie)
                .GroupBy(p =>
                {
                    var dernier = Jalons[0];
                    foreach (var j in Jalons)
                    {
                        if (Jalon(p, j.Cle) is not null)
                        {
                            dernier = j;
                        }
                    }

                    return dernier.Libelle;
                })
                .Select(x => new { etape = x.Key, nombre = x.Count() })
                .OrderByDescending(x => x.nombre).ToList();

            return Results.Ok(new { total = parcours.Count, entonnoir, transitions, pertes });
        }).WithSummary("Entonnoir, délais observés par transition et points de perte.");

        // -------------------------------------------------------------- charge des équipes
        g.MapGet("/charge", async (
                VaeDbContext db, ParametresService parametres, CancellationToken ct) =>
        {
            var capaciteAap = await parametres.EntierAsync("capacite.aap", 12, ct);
            var capaciteAcc = await parametres.EntierAsync("capacite.accompagnateur", 6, ct);
            var aujourdHui = DateOnly.FromDateTime(DateTime.UtcNow);

            var actifs = await db.Parcours
                .Where(p => p.Etape != EtapeParcours.Cloture && p.Etape != EtapeParcours.Sortie)
                .Include(p => p.Alertes.Where(a => a.ResolueLe == null))
                .ToListAsync(ct);

            var intervenants = await db.Intervenants
                .Where(i => i.Statut == StatutIntervenant.Actif || i.Statut == StatutIntervenant.EnIntegration)
                .ToListAsync(ct);

            var aaps = intervenants
                .Where(i => i.Type == TypeIntervenant.ArchitecteAccompagnateurParcours)
                .Select(i =>
                {
                    var siens = actifs.Where(p => p.AapId == i.Id).ToList();
                    var capacite = i.CapaciteCandidats ?? capaciteAap;
                    return new
                    {
                        i.Id, nom = i.NomComplet,
                        actifs = siens.Count,
                        nouveaux = siens.Count(p => p.DateDemande is { } d
                                                    && aujourdHui.DayNumber - d.DayNumber <= 60),
                        faisabilite = siens.Count(p => p.Etape is >= EtapeParcours.Financement
                                                          and <= EtapeParcours.ParcoursValide),
                        accompagnement = siens.Count(p => p.Etape is EtapeParcours.Accompagnement
                                                             or EtapeParcours.PreparationJury),
                        juryProche = siens.Count(p => p.DateJury is { } j && j > aujourdHui
                                                      && j.DayNumber - aujourdHui.DayNumber <= 90),
                        alertesCritiques = siens.Sum(p => p.Alertes.Count(
                            a => a.EstOuverte && a.Severite == SeveriteAlerte.Critique)),
                        capacite,
                        tauxCharge = capacite == 0 ? 0m : Math.Round((decimal)siens.Count / capacite, 3),
                    };
                })
                .OrderByDescending(x => x.actifs).ToList();

            var heuresParIntervenant = await db.Seances
                .Where(s => s.IntervenantId != null && s.Realisee)
                .GroupBy(s => s.IntervenantId!.Value)
                .Select(x => new { Id = x.Key, H = x.Sum(s => s.DureeHeures) })
                .ToDictionaryAsync(x => x.Id, x => x.H, ct);

            var accompagnateurs = intervenants
                .Where(i => i.Type == TypeIntervenant.Accompagnateur)
                .Select(i =>
                {
                    var siens = actifs.Where(p => p.AccompagnateurId == i.Id).ToList();
                    var capacite = i.CapaciteCandidats ?? capaciteAcc;
                    return new
                    {
                        i.Id, nom = i.NomComplet, i.Specialites, i.Region, i.Territoire,
                        i.InterventionDistanciel, i.TarifHoraire,
                        actifs = siens.Count,
                        heuresPrescrites = siens.Sum(p => p.HeuresPrescritesTotales),
                        heuresRealisees = heuresParIntervenant.GetValueOrDefault(i.Id),
                        capacite,
                        tauxCharge = capacite == 0 ? 0m : Math.Round((decimal)siens.Count / capacite, 3),
                        statut = i.Statut.ToString(),
                    };
                })
                .OrderByDescending(x => x.actifs).ToList();

            var certificationsNonCouvertes = await db.Parcours
                .Where(p => p.Etape != EtapeParcours.Cloture && p.Etape != EtapeParcours.Sortie
                            && p.CertificationId != null
                            && !db.Habilitations.Any(h => h.CertificationId == p.CertificationId))
                .Select(p => new { p.Certification!.Id, p.Certification.CodeRncp, p.Certification.Intitule })
                .Distinct().ToListAsync(ct);

            return Results.Ok(new
            {
                capaciteAap, capaciteAcc,
                dossiersActifs = actifs.Count,
                sansAap = actifs.Count(p => p.AapId is null),
                capaciteDisponible = Math.Max(0, aaps.Count * capaciteAap - actifs.Count),
                aaps, accompagnateurs, certificationsNonCouvertes,
            });
        }).WithSummary("Charge des AAP et des accompagnateurs, capacité disponible, certifications non couvertes.");

        // -------------------------------------------------------------- heures
        g.MapGet("/heures", async (
                [AsParameters] FiltreTableauDeBord filtre, ServiceTableauDeBord service,
                ParametresService parametres, CancellationToken ct) =>
        {
            var tarification = await parametres.TarificationAsync(ct);
            var parcours = await service.Requete(filtre).AsSplitQuery().ToListAsync(ct);
            var avecHeures = parcours.Where(p => p.HeuresPrescritesTotales > 0).ToList();

            decimal Realisees(Parcours p) => p.Seances
                .Where(s => s.Realisee && s.Nature != NatureHeure.Asynchrone).Sum(s => s.DureeHeures);

            var lignes = avecHeures.Select(p =>
            {
                var grille = tarification.GrillePour(p);
                var realisees = Realisees(p);
                var prescrites = p.HeuresPrescritesTotales;
                return new
                {
                    p.Id,
                    candidat = $"{p.Candidat?.Prenom} {p.Candidat?.Nom}".Trim(),
                    certification = p.Certification?.Abrege ?? p.Certification?.Intitule,
                    etape = MoteurAlertes.Libelle(p.Etape),
                    individuel = p.HeuresIndividuelPrescrites,
                    collectif = p.HeuresCollectifPrescrites,
                    complement = p.HeuresComplementFormatifPrescrites,
                    prescrites, realisees,
                    restantes = Math.Max(0, prescrites - realisees),
                    taux = prescrites == 0 ? 0m : Math.Round(realisees / prescrites, 3),
                    seances = p.Seances.Count(s => s.Realisee),
                    depassementIndividuel = p.HeuresIndividuelPrescrites > grille.PlafondHeuresIndividuel,
                    depassementCollectif = p.HeuresCollectifPrescrites > grille.PlafondHeuresCollectif,
                    depassementComplement =
                        p.HeuresComplementFormatifPrescrites > grille.PlafondHeuresComplementFormatif,
                    grille = grille.Code,
                };
            }).OrderByDescending(x => x.taux).ToList();

            var grilleCourante = tarification.GrillePour(DateOnly.FromDateTime(DateTime.UtcNow));

            return Results.Ok(new
            {
                totalIndividuel = avecHeures.Sum(p => p.HeuresIndividuelPrescrites),
                totalCollectif = avecHeures.Sum(p => p.HeuresCollectifPrescrites),
                totalComplement = avecHeures.Sum(p => p.HeuresComplementFormatifPrescrites),
                totalPrescrites = avecHeures.Sum(p => p.HeuresPrescritesTotales),
                totalRealisees = avecHeures.Sum(Realisees),
                plafonds = new
                {
                    individuel = grilleCourante.PlafondHeuresIndividuel,
                    collectif = grilleCourante.PlafondHeuresCollectif,
                    complement = grilleCourante.PlafondHeuresComplementFormatif,
                    montant = grilleCourante.PlafondMontantTotal,
                },
                lignes,
            });
        }).WithSummary("Heures prescrites, réalisées et restantes, avec les plafonds opposables.");

        // -------------------------------------------------------------- qualité
        g.MapGet("/qualite", async (
                [AsParameters] FiltreTableauDeBord filtre, ServiceTableauDeBord service, CancellationToken ct) =>
        {
            var parcours = await service.Requete(filtre).AsSplitQuery().ToListAsync(ct);
            var actifs = parcours.Where(p => p.Etape != EtapeParcours.Sortie).ToList();

            (string Code, string Libelle, bool Critique, Func<Parcours, bool> Test)[] controles =
            [
                ("Q1", "AAP référent identifié", true, p => p.AapId is not null),
                ("Q2", "Date de premier contact tracée", true, p => p.DatePremierContact is not null),
                ("Q3", "RDV pédagogique tracé", false, p => p.DateRecueilBesoins is not null),
                ("Q4", "Dossier de faisabilité déposé", false, p => p.DateDepotFaisabilite is not null),
                ("Q5", "Financement sécurisé", true, p => p.Financements.Any(f => f.EstSecurise)),
                ("Q6", "Heures prescrites renseignées", true, p => p.HeuresPrescritesTotales > 0),
                ("Q7", "Dossier Solei créé", false, p => !string.IsNullOrWhiteSpace(p.CodeAcfSolei)),
                ("Q8", "Accompagnateur affecté", false, p => p.AccompagnateurId is not null),
                ("Q9", "Séances tracées", false, p => p.Seances.Count > 0),
                ("Q10", "Consentement RGPD recueilli", true, p => p.Candidat?.ConsentementRgpd ?? false),
            ];

            decimal Score(Parcours p) => controles.Count(c => c.Test(p)) / (decimal)controles.Length;

            var aRisque = actifs
                .Where(p => controles.Count(c => c.Critique && !c.Test(p)) >= 2)
                .OrderBy(Score)
                .Select(p => new
                {
                    p.Id,
                    candidat = $"{p.Candidat?.Prenom} {p.Candidat?.Nom}".Trim(),
                    certification = p.Certification?.Abrege ?? p.Certification?.Intitule,
                    etape = MoteurAlertes.Libelle(p.Etape),
                    completude = Math.Round(Score(p), 3),
                    manquants = controles.Where(c => !c.Test(p))
                        .Select(c => new { c.Code, c.Libelle, c.Critique }),
                }).ToList();

            var aujourdHui = DateOnly.FromDateTime(DateTime.UtcNow);
            var juryPasses = parcours.Count(p => p.ResultatJury != ResultatJury.NonRenseigne
                                                 || (p.DateJury is not null && p.DateJury <= aujourdHui));

            return Results.Ok(new
            {
                dossiers = actifs.Count,
                complets = actifs.Count(p => Score(p) == 1),
                completudeMoyenne = actifs.Count == 0 ? 0 : Math.Round(actifs.Average(Score), 3),
                aRisque = aRisque.Count,
                controles = controles.Select(c => new
                {
                    c.Code, c.Libelle, c.Critique,
                    satisfaits = actifs.Count(c.Test),
                    total = actifs.Count,
                }),
                resultats = new
                {
                    validationTotale = parcours.Count(p => p.ResultatJury == ResultatJury.ValidationTotale),
                    validationPartielle = parcours.Count(p => p.ResultatJury == ResultatJury.ValidationPartielle),
                    refus = parcours.Count(p => p.ResultatJury == ResultatJury.Refus),
                    nonRenseigne = parcours.Count(p => p.ResultatJury == ResultatJury.NonRenseigne
                                                       && p.DateJury is not null && p.DateJury <= aujourdHui),
                    juryPasses,
                },
                sorties = parcours.Where(p => p.Etape == EtapeParcours.Sortie)
                    .GroupBy(p => ServiceTableauDeBord.LibelleSortie(p.MotifSortie))
                    .Select(x => new { motif = string.IsNullOrEmpty(x.Key) ? "Non renseigné" : x.Key, nombre = x.Count() }),
                dossiersARisque = aRisque,
            });
        }).WithSummary("Complétude documentaire, résultats de jury et dossiers à sécuriser.");

        // -------------------------------------------------------------- finance
        g.MapGet("/finance", async (
                [AsParameters] FiltreTableauDeBord filtre, ServiceTableauDeBord service,
                ParametresService parametres, VaeDbContext db, CancellationToken ct) =>
        {
            var tarification = await parametres.TarificationAsync(ct);
            var parcours = await service.Requete(filtre).AsSplitQuery().ToListAsync(ct);
            var actifs = parcours.Where(p => p.EstActif).ToList();

            var factures = await db.Factures.AsNoTracking().ToListAsync(ct);

            var parGrille = actifs.GroupBy(p => tarification.GrillePour(p).Code)
                .Select(x => new
                {
                    grille = x.Key,
                    dossiers = x.Count(),
                    montant = x.Sum(p => tarification.Calculer(p).Total),
                    marge = x.Sum(p => tarification.Calculer(p).Marge),
                }).OrderBy(x => x.grille).ToList();

            var parCertification = actifs.Where(p => p.Certification is not null)
                .GroupBy(p => p.Certification!.Abrege ?? p.Certification.Intitule)
                .Select(x => new { certification = x.Key, montant = x.Sum(p => tarification.Calculer(p).Total) })
                .OrderByDescending(x => x.montant).ToList();

            var parFinanceur = factures.GroupBy(f => f.Financeur ?? "Non renseigné")
                .Select(x => new { financeur = x.Key, montant = x.Sum(f => f.MontantHt), nombre = x.Count() })
                .OrderByDescending(x => x.montant).ToList();

            var lignes = actifs.Select(p =>
            {
                var c = tarification.Calculer(p);
                return new
                {
                    p.Id,
                    candidat = $"{p.Candidat?.Prenom} {p.Candidat?.Nom}".Trim(),
                    certification = p.Certification?.Abrege ?? p.Certification?.Intitule,
                    grille = c.CodeGrille, c.Forfait, c.MontantIndividuel, c.MontantCollectif,
                    c.MontantComplementFormatif, c.FraisJury, c.Total, c.CoutPedagogique, c.Marge, c.TauxMarge,
                    depassements = c.Depassements,
                };
            }).OrderByDescending(x => x.Marge).ToList();

            return Results.Ok(new
            {
                caPrevisionnel = actifs.Sum(p => tarification.Calculer(p).Total),
                margePrevisionnelle = actifs.Sum(p => tarification.Calculer(p).Marge),
                caFacture = factures.Sum(f => f.MontantHt),
                caEncaisse = factures.Where(f => f.EstReglee).Sum(f => f.MontantHt),
                enAttente = factures.Where(f => !f.EstReglee).Sum(f => f.MontantHt),
                nombreFactures = factures.Count,
                panierMoyen = factures.Count == 0 ? 0 : Math.Round(factures.Sum(f => f.MontantHt) / factures.Count, 2),
                parGrille, parCertification, parFinanceur, lignes,
            });
        }).WithSummary("Chiffre d'affaires, marge et rentabilité par parcours, ventilés par grille tarifaire.");

        // -------------------------------------------------------------- simulateur
        g.MapGet("/simuler", async (
                DateOnly? dateDebut, bool forfait, decimal individuel, decimal collectif,
                decimal complement, bool jury, int participants,
                ParametresService parametres, CancellationToken ct) =>
        {
            var tarification = await parametres.TarificationAsync(ct);
            var date = dateDebut ?? DateOnly.FromDateTime(DateTime.UtcNow);
            var grille = tarification.GrillePour(date);

            var calcul = tarification.Calculer(
                grille, forfait, individuel, collectif, complement, jury, Math.Max(1, participants));

            var caParHeure = tarification.CaParHeureMobilisee(
                grille, forfait, individuel, collectif, complement, jury, Math.Max(1, participants));

            return Results.Ok(new
            {
                grille = new
                {
                    grille.Code, grille.Libelle, grille.DateEffet, grille.DateFin,
                    grille.ForfaitArchitecture, grille.TarifHoraireIndividuel, grille.TarifHoraireCollectif,
                    grille.TarifHoraireComplementFormatif, grille.FraisJury, grille.PlafondMontantTotal,
                },
                dateDebut = date,
                calcul.Forfait, calcul.MontantIndividuel, calcul.MontantCollectif,
                calcul.MontantComplementFormatif, calcul.FraisJury, calcul.Total,
                calcul.CoutPedagogique, calcul.Marge, calcul.TauxMarge,
                parCandidat = Math.Round(calcul.Total / Math.Max(1, participants), 2),
                caParHeureMobilisee = caParHeure,
                calcul.PlafondRespecte, calcul.Depassements,
            });
        }).WithSummary("Simule un parcours à la grille en vigueur à la date de démarrage indiquée.");
    }
}
