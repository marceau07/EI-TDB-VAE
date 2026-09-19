using System.Text.Json;
using System.Text.Json.Serialization;
using EiVae.Domain;
using EiVae.Domain.Entities;
using EiVae.Domain.Services;
using EiVae.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace EiVae.Infrastructure.Seeding;

public sealed record ResultatAmorcage(
    int Certifications, int Blocs, int Intervenants, int Candidats, int Parcours,
    int Financements, int Factures, int Seances, string Message);

/// <summary>
/// Charge la reprise de données au premier démarrage : le référentiel des
/// certifications extrait de France Compétences, puis les dossiers, intervenants
/// et factures repris des fichiers de suivi du service.
///
/// Strictement idempotent : chaque entité est rapprochée sur une clé naturelle
/// — code RNCP, nom d'intervenant, numéro de facture, couple candidat et
/// certification. Relancer l'amorçage ne crée aucun doublon.
/// </summary>
public sealed class DonneesInitialesSeeder(
    VaeDbContext db,
    ParametresService parametres,
    IConfiguration configuration,
    ILogger<DonneesInitialesSeeder> logger)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    public async Task<ResultatAmorcage> ChargerAsync(CancellationToken ct = default)
    {
        var dossier = configuration["Base:DossierSeed"]
                      ?? Path.Combine(AppContext.BaseDirectory, "seed");

        if (!Directory.Exists(dossier))
        {
            return new ResultatAmorcage(0, 0, 0, 0, 0, 0, 0, 0,
                $"Aucun dossier de reprise trouvé ({dossier}).");
        }

        var certifications = await ChargerCertificationsAsync(dossier, ct).ConfigureAwait(false);
        var metier = await ChargerDonneesMetierAsync(dossier, ct).ConfigureAwait(false);

        return new ResultatAmorcage(
            certifications.Certifications, certifications.Blocs,
            metier.Intervenants, metier.Candidats, metier.Parcours,
            metier.Financements, metier.Factures, metier.Seances,
            "Amorçage terminé.");
    }

    // ------------------------------------------------------------------ certifications
    private async Task<(int Certifications, int Blocs)> ChargerCertificationsAsync(
        string dossier, CancellationToken ct)
    {
        var fichier = Path.Combine(dossier, "certifications.json");
        if (!File.Exists(fichier))
        {
            logger.LogInformation("Aucun fichier certifications.json : le référentiel sera alimenté "
                                  + "par la synchronisation France Compétences.");
            return (0, 0);
        }

        await using var flux = File.OpenRead(fichier);
        var contenu = await JsonSerializer.DeserializeAsync<FichierCertifications>(flux, Json, ct)
            .ConfigureAwait(false);

        if (contenu?.Certifications is not { Count: > 0 })
        {
            return (0, 0);
        }

        var existantes = await db.Certifications.Select(c => c.CodeRncp)
            .ToHashSetAsync(StringComparer.OrdinalIgnoreCase, ct).ConfigureAwait(false);

        var certificateurs = await db.Certificateurs
            .ToDictionaryAsync(c => c.Siret ?? c.Nom, StringComparer.OrdinalIgnoreCase, ct)
            .ConfigureAwait(false);

        int nouvelles = 0, blocs = 0;

        foreach (var c in contenu.Certifications)
        {
            if (string.IsNullOrWhiteSpace(c.CodeRncp) || existantes.Contains(c.CodeRncp))
            {
                continue;
            }

            var certification = new Certification
            {
                CodeRncp = c.CodeRncp,
                IdFiche = c.IdFiche,
                Intitule = c.Intitule ?? c.CodeRncp,
                EtatFiche = c.EtatFiche,
                ActifFranceCompetences = c.Actif,
                Niveau = c.Niveau,
                LibelleNiveau = c.LibelleNiveau,
                TypeEnregistrement = c.TypeEnregistrement,
                DateDecision = Date(c.DateDecision),
                DateFinEnregistrement = Date(c.DateFinEnregistrement),
                DateLimiteDelivrance = Date(c.DateLimiteDelivrance),
                DateDerniereModificationFiche = Date(c.DateDerniereModification),
                VoieVaeOuverte = c.VoiesAcces?.Vae ?? false,
                CompositionJuryVae = c.CompositionJuryVae,
                VoieFormationInitiale = c.VoiesAcces?.FormationInitiale ?? false,
                VoieFormationContinue = c.VoiesAcces?.FormationContinue ?? false,
                VoieApprentissage = c.VoiesAcces?.Apprentissage ?? false,
                VoieContratProfessionnalisation = c.VoiesAcces?.ContratProfessionnalisation ?? false,
                VoieCandidatLibre = c.VoiesAcces?.CandidatLibre ?? false,
                ActivitesVisees = c.ActivitesVisees,
                CapacitesAttestees = c.CapacitesAttestees,
                SecteursActivite = c.SecteursActivite,
                TypeEmploiAccessibles = c.TypeEmploiAccessibles,
                ObjectifsContexte = c.ObjectifsContexte,
                Prerequis = c.Prerequis,
                ReglementationActivites = c.ReglementationActivites,
                CodesNsfJson = Serialiser(c.CodesNsf),
                FormacodesJson = Serialiser(c.Formacodes),
                CodesRomeJson = Serialiser(c.CodesRome),
                StatistiquesJson = Serialiser(c.Statistiques),
                DomaineEi = c.DomaineEi,
                StatutInterne = StatutCertificationInterne.NonCouverte,
                DerniereSynchronisation = DateTimeOffset.UtcNow,
            };

            var ordre = 0;
            foreach (var b in c.Blocs ?? [])
            {
                if (string.IsNullOrWhiteSpace(b.Code))
                {
                    continue;
                }

                certification.Blocs.Add(new BlocCompetences
                {
                    Code = b.Code,
                    Libelle = b.Libelle ?? b.Code,
                    Competences = b.Competences,
                    ModalitesEvaluation = b.ModalitesEvaluation,
                    Ordre = ++ordre,
                });
                blocs++;
            }

            var premier = true;
            foreach (var ce in c.Certificateurs ?? [])
            {
                if (string.IsNullOrWhiteSpace(ce.Nom))
                {
                    continue;
                }

                // Un SIRET vide doit rester NULL : l'index unique rejetterait
                // la deuxieme chaine vide inseree.
                var siret = string.IsNullOrWhiteSpace(ce.Siret) ? null : ce.Siret.Trim();
                var cle = siret ?? ce.Nom;

                if (!certificateurs.TryGetValue(cle, out var certificateur))
                {
                    certificateur = new Certificateur { Nom = ce.Nom, Siret = siret, Etat = ce.Etat };
                    db.Certificateurs.Add(certificateur);
                    certificateurs[cle] = certificateur;
                }

                certification.Certificateurs.Add(new CertificationCertificateur
                {
                    Certification = certification,
                    Certificateur = certificateur,
                    EstPrincipal = premier,
                });
                premier = false;
            }

            db.Certifications.Add(certification);
            existantes.Add(c.CodeRncp);
            nouvelles++;
        }

        if (nouvelles > 0)
        {
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            logger.LogInformation("{N} certifications et {B} blocs de compétences chargés.", nouvelles, blocs);
        }

        return (nouvelles, blocs);
    }

    // ------------------------------------------------------------------ données métier
    private async Task<(int Intervenants, int Candidats, int Parcours, int Financements, int Factures, int Seances)>
        ChargerDonneesMetierAsync(string dossier, CancellationToken ct)
    {
        var fichier = Path.Combine(dossier, "reprise.json");
        if (!File.Exists(fichier))
        {
            return (0, 0, 0, 0, 0, 0);
        }

        if (await db.Parcours.AnyAsync(ct).ConfigureAwait(false))
        {
            logger.LogInformation("La base contient déjà des dossiers : la reprise n'est pas rejouée.");
            return (0, 0, 0, 0, 0, 0);
        }

        await using var flux = File.OpenRead(fichier);
        var contenu = await JsonSerializer.DeserializeAsync<FichierReprise>(flux, Json, ct).ConfigureAwait(false);
        if (contenu is null)
        {
            return (0, 0, 0, 0, 0, 0);
        }

        var tarification = await parametres.TarificationAsync(ct).ConfigureAwait(false);

        // ---- intervenants ----
        var parNom = new Dictionary<string, Intervenant>(StringComparer.OrdinalIgnoreCase);
        foreach (var i in contenu.Intervenants ?? [])
        {
            if (string.IsNullOrWhiteSpace(i.Nom))
            {
                continue;
            }

            var intervenant = new Intervenant
            {
                Nom = i.Nom,
                Prenom = i.Prenom,
                Type = Enum.TryParse<TypeIntervenant>(i.Type, true, out var t) ? t : TypeIntervenant.Accompagnateur,
                Statut = Enum.TryParse<StatutIntervenant>(i.Statut, true, out var s) ? s : StatutIntervenant.Actif,
                Email = i.Email,
                Telephone = i.Telephone,
                Territoire = i.Territoire,
                Region = i.Region,
                InterventionDistanciel = i.Distanciel ?? true,
                Specialites = i.Specialites,
                TarifHoraire = i.TarifHoraire,
                Notes = i.Notes,
            };

            db.Intervenants.Add(intervenant);
            parNom[intervenant.NomComplet] = intervenant;
            if (!string.IsNullOrWhiteSpace(i.Cle))
            {
                parNom[i.Cle] = intervenant;
            }
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        var certificationsParCode = await db.Certifications
            .ToDictionaryAsync(c => c.CodeRncp, StringComparer.OrdinalIgnoreCase, ct).ConfigureAwait(false);

        // ---- dossiers ----
        int candidats = 0, parcoursCrees = 0, financements = 0, factures = 0, seances = 0;

        // Le rapprochement des factures se fait sur le nom du candidat : deux
        // homonymes recevraient la meme facture. Le numero de facture etant
        // unique, on ne l'attache qu'au premier dossier rencontre et on signale
        // les autres plutot que d'echouer.
        var numerosUtilises = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var d in contenu.Parcours ?? [])
        {
            if (string.IsNullOrWhiteSpace(d.Nom))
            {
                continue;
            }

            var candidat = new Candidat
            {
                Nom = d.Nom.ToUpperInvariant(),
                Prenom = d.Prenom ?? string.Empty,
                Email = d.Email,
                Telephone = d.Telephone,
                Ville = d.Ville,
                Departement = d.Departement,
                Region = d.Region,
                ConsentementRgpd = false,
            };

            Certification? certification = null;
            if (!string.IsNullOrWhiteSpace(d.CodeRncp))
            {
                certificationsParCode.TryGetValue(d.CodeRncp, out certification);
            }

            if (certification is not null && !string.IsNullOrWhiteSpace(d.Abrege)
                && string.IsNullOrWhiteSpace(certification.Abrege))
            {
                // L'abrégé du service — « TP FPA », « DE EJE » — est une donnée
                // interne : la reprise est la seule occasion de le renseigner.
                certification.Abrege = d.Abrege;
                certification.StatutInterne = StatutCertificationInterne.Active;
            }

            var parcours = new Parcours
            {
                Candidat = candidat,
                CertificationId = certification?.Id,
                Etape = Enum.TryParse<EtapeParcours>(d.Etape, true, out var e) ? e : EtapeParcours.Qualification,
                StatutSecondaire = Enum.TryParse<StatutSecondaire>(d.StatutSecondaire, true, out var ss)
                    ? ss : StatutSecondaire.Aucun,
                MotifSortie = Enum.TryParse<MotifSortie>(d.MotifSortie, true, out var ms) ? ms : MotifSortie.Aucun,
                Origine = Enum.TryParse<OrigineCandidature>(d.Origine, true, out var o)
                    ? o : OrigineCandidature.FranceVae,
                ResultatJury = Enum.TryParse<ResultatJury>(d.ResultatJury, true, out var rj)
                    ? rj : ResultatJury.NonRenseigne,
                AapId = Intervenant(parNom, d.Aap)?.Id,
                AccompagnateurId = Intervenant(parNom, d.Accompagnateur)?.Id,
                GestionnaireId = Intervenant(parNom, d.Gestionnaire)?.Id,
                DateDemande = Date(d.DateDemande),
                DatePremierContact = Date(d.DatePremierContact),
                DateRecueilBesoins = Date(d.DateRecueilBesoins),
                DateRdvFaisabilite = Date(d.DateRdvFaisabilite),
                DateDepotFaisabilite = Date(d.DateDepotFaisabilite),
                DateParcoursValide = Date(d.DateParcoursValide),
                DateRecevabilite = Date(d.DateRecevabilite),
                DateDepotDossierValidation = Date(d.DateDepotDossier),
                DateJury = Date(d.DateJury),
                DateEntretienPostJury = Date(d.DatePostJury),
                DateDernierMouvement = Date(d.DateDernierMouvement),
                HeuresIndividuelPrescrites = d.HeuresIndividuel ?? 0,
                HeuresCollectifPrescrites = d.HeuresCollectif ?? 0,
                HeuresComplementFormatifPrescrites = d.HeuresComplement ?? 0,
                ForfaitArchitectureApplique = d.ForfaitArchitecture ?? true,
                FraisJuryInclus = d.FraisJury ?? false,
                CodeAcfSolei = d.CodeAcf,
                Historique = d.Historique,
            };

            parcours.DateDebutParcours = Date(d.DateDebutParcours) ?? parcours.CalculerDateDebut();
            if (parcours.DateDebutParcours is { } debut)
            {
                parcours.GrilleTarifaireId = tarification.GrillePour(debut).Id;
            }

            foreach (var f in d.Financements ?? [])
            {
                parcours.Financements.Add(new Financement
                {
                    Dispositif = Enum.TryParse<DispositifFinancement>(f.Dispositif, true, out var dp)
                        ? dp : DispositifFinancement.NonSecurise,
                    Financeur = f.Financeur,
                    NumeroPriseEnCharge = f.NumeroPriseEnCharge,
                    MontantAccorde = f.Montant,
                    DateSecurisation = Date(f.DateSecurisation),
                });
                financements++;
            }

            foreach (var f in d.Factures ?? [])
            {
                if (string.IsNullOrWhiteSpace(f.Numero))
                {
                    continue;
                }

                if (!numerosUtilises.Add(f.Numero))
                {
                    logger.LogWarning(
                        "Facture {Numero} deja rattachee : ignoree pour {Nom} {Prenom}. "
                        + "A verifier manuellement, il s'agit probablement d'homonymes.",
                        f.Numero, candidat.Nom, candidat.Prenom);
                    continue;
                }

                parcours.Factures.Add(new Facture
                {
                    Numero = f.Numero,
                    DateEmission = Date(f.DateEmission) ?? DateOnly.FromDateTime(DateTime.UtcNow),
                    Financeur = f.Financeur,
                    MontantHt = f.MontantHt ?? 0,
                    DateReglement = Date(f.DateReglement),
                    CodeAcf = f.CodeAcf,
                });
                factures++;
            }

            foreach (var s in d.Seances ?? [])
            {
                var date = Date(s.Date);
                if (date is null)
                {
                    continue;
                }

                parcours.Seances.Add(new Seance
                {
                    Date = date.Value,
                    Nature = Enum.TryParse<NatureHeure>(s.Nature, true, out var n) ? n : NatureHeure.Individuel,
                    DureeHeures = s.DureeHeures ?? 0,
                    Realisee = true,
                    Objet = s.Objet,
                });
                seances++;
            }

            db.Candidats.Add(candidat);
            db.Parcours.Add(parcours);
            candidats++;
            parcoursCrees++;
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        logger.LogInformation(
            "Reprise : {C} candidats, {P} dossiers, {F} financements, {Fa} factures, {S} séances.",
            candidats, parcoursCrees, financements, factures, seances);

        return (parNom.Values.Distinct().Count(), candidats, parcoursCrees, financements, factures, seances);
    }

    private static Intervenant? Intervenant(Dictionary<string, Intervenant> index, string? cle) =>
        string.IsNullOrWhiteSpace(cle) ? null : index.GetValueOrDefault(cle);

    private static DateOnly? Date(string? v) =>
        string.IsNullOrWhiteSpace(v) ? null
        : DateOnly.TryParse(v, System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : null;

    private static string? Serialiser(object? v) =>
        v is null ? null : JsonSerializer.Serialize(v, Json);

    // ------------------------------------------------------------------ contrats de fichier
    private sealed record FichierCertifications(List<CertificationSeed>? Certifications);

    private sealed record CertificationSeed(
        string? CodeRncp, string? IdFiche, string? Intitule, string? EtatFiche, bool Actif,
        int? Niveau, string? LibelleNiveau, string? TypeEnregistrement,
        string? DateDecision, string? DateFinEnregistrement, string? DateLimiteDelivrance,
        string? DateDerniereModification, VoiesAccesSeed? VoiesAcces, string? CompositionJuryVae,
        List<BlocSeed>? Blocs, List<CertificateurSeed>? Certificateurs,
        string? ActivitesVisees, string? CapacitesAttestees, string? SecteursActivite,
        string? TypeEmploiAccessibles, string? ObjectifsContexte, string? Prerequis,
        string? ReglementationActivites, JsonElement? CodesNsf, JsonElement? Formacodes,
        JsonElement? CodesRome, JsonElement? Statistiques, string? DomaineEi);

    private sealed record VoiesAccesSeed(
        bool Vae, bool FormationInitiale, bool FormationContinue, bool Apprentissage,
        bool ContratProfessionnalisation, bool CandidatLibre);

    private sealed record BlocSeed(string? Code, string? Libelle, string? Competences, string? ModalitesEvaluation);

    private sealed record CertificateurSeed(string? Nom, string? Siret, string? Etat);

    private sealed record FichierReprise(List<IntervenantSeed>? Intervenants, List<ParcoursSeed>? Parcours);

    private sealed record IntervenantSeed(
        string? Cle, string? Nom, string? Prenom, string? Type, string? Statut, string? Email,
        string? Telephone, string? Territoire, string? Region, bool? Distanciel,
        string? Specialites, decimal? TarifHoraire, string? Notes);

    private sealed record ParcoursSeed(
        string? Nom, string? Prenom, string? Email, string? Telephone, string? Ville,
        string? Departement, string? Region, string? CodeRncp, string? Abrege,
        string? Etape, string? StatutSecondaire, string? MotifSortie, string? Origine, string? ResultatJury,
        string? Aap, string? Accompagnateur, string? Gestionnaire,
        string? DateDemande, string? DatePremierContact, string? DateRecueilBesoins,
        string? DateRdvFaisabilite, string? DateDepotFaisabilite, string? DateParcoursValide,
        string? DateRecevabilite, string? DateDepotDossier, string? DateJury, string? DatePostJury,
        string? DateDebutParcours, string? DateDernierMouvement,
        decimal? HeuresIndividuel, decimal? HeuresCollectif, decimal? HeuresComplement,
        bool? ForfaitArchitecture, bool? FraisJury, string? CodeAcf, string? Historique,
        List<FinancementSeed>? Financements, List<FactureSeed>? Factures, List<SeanceSeed>? Seances);

    private sealed record FinancementSeed(
        string? Dispositif, string? Financeur, string? NumeroPriseEnCharge,
        decimal? Montant, string? DateSecurisation);

    private sealed record FactureSeed(
        string? Numero, string? DateEmission, string? Financeur, decimal? MontantHt,
        string? DateReglement, string? CodeAcf);

    private sealed record SeanceSeed(string? Date, string? Nature, decimal? DureeHeures, string? Objet);
}
