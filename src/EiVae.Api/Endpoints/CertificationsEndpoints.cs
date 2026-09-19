using System.Text.Json;
using EiVae.Domain;
using EiVae.Domain.Entities;
using EiVae.Infrastructure;
using EiVae.Infrastructure.Integrations.FranceCompetences;
using Microsoft.EntityFrameworkCore;

namespace EiVae.Api.Endpoints;

/// <summary>
/// Champs internes d'une certification. Seuls ceux-ci sont modifiables :
/// tout ce qui vient de France Compétences est en lecture seule et serait
/// écrasé à la synchronisation suivante.
/// </summary>
public sealed record SaisieCertificationInterne(
    string? Abrege,
    string? DomaineEi,
    StatutCertificationInterne StatutInterne,
    int? DureeHabituelleJours,
    string? Particularites,
    string? ContactCertificateurNom,
    string? ContactCertificateurEmail,
    string? ContactCertificateurTelephone,
    int[]? ModulesAcademie,
    int[]? ExpertsMetierIds);

public sealed record SaisieCertificationManuelle(string CodeRncp, string? Abrege, string? DomaineEi);

public static class CertificationsEndpoints
{
    public static void MapCertificationsEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/certifications").WithTags("Référentiel certifications");

        g.MapGet("/", async (
                string? domaine, int? niveau, bool? avecDemandes, bool? vaeSeulement,
                string? recherche, VaeDbContext db, CancellationToken ct) =>
        {
            var q = db.Certifications.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(domaine))
            {
                q = q.Where(c => c.DomaineEi == domaine);
            }

            if (niveau is { } n)
            {
                q = q.Where(c => c.Niveau == n);
            }

            if (vaeSeulement is true)
            {
                q = q.Where(c => c.VoieVaeOuverte);
            }

            if (!string.IsNullOrWhiteSpace(recherche))
            {
                var r = recherche.Trim();
                q = q.Where(c => EF.Functions.ILike(c.Intitule, $"%{r}%")
                                 || EF.Functions.ILike(c.CodeRncp, $"%{r}%")
                                 || (c.Abrege != null && EF.Functions.ILike(c.Abrege, $"%{r}%")));
            }

            var demandes = await db.Parcours
                .Where(p => p.CertificationId != null)
                .GroupBy(p => p.CertificationId!.Value)
                .Select(x => new
                {
                    Id = x.Key,
                    Total = x.Count(),
                    Actifs = x.Count(p => p.Etape != EtapeParcours.Cloture && p.Etape != EtapeParcours.Sortie),
                })
                .ToDictionaryAsync(x => x.Id, x => x, ct);

            var couverture = await db.Habilitations
                .GroupBy(h => h.CertificationId)
                .Select(x => new { Id = x.Key, N = x.Count() })
                .ToDictionaryAsync(x => x.Id, x => x.N, ct);

            var liste = await q.OrderBy(c => c.Intitule).ToListAsync(ct);

            var resultat = liste.Select(c =>
            {
                var d = demandes.GetValueOrDefault(c.Id);
                return new
                {
                    c.Id, c.CodeRncp, c.Intitule, c.Abrege, c.Niveau, c.LibelleNiveau, c.DomaineEi,
                    c.TypeEnregistrement, c.VoieVaeOuverte, c.ActifFranceCompetences,
                    c.DateFinEnregistrement, c.DerniereSynchronisation,
                    statutInterne = c.StatutInterne.ToString(),
                    lien = c.LienFranceCompetences,
                    demandes = d?.Total ?? 0,
                    actifs = d?.Actifs ?? 0,
                    intervenantsHabilites = couverture.GetValueOrDefault(c.Id),
                };
            });

            if (avecDemandes is true)
            {
                resultat = resultat.Where(x => x.demandes > 0);
            }
            else if (avecDemandes is false)
            {
                resultat = resultat.Where(x => x.demandes == 0);
            }

            return Results.Ok(resultat.ToList());
        }).WithSummary("Référentiel des certifications, avec la demande observée et la couverture du réseau.");

        g.MapGet("/{id:int}", async (int id, VaeDbContext db, CancellationToken ct) =>
        {
            var c = await db.Certifications.AsNoTracking()
                .Include(x => x.Blocs)
                .Include(x => x.Certificateurs).ThenInclude(cc => cc.Certificateur)
                .Include(x => x.Habilitations).ThenInclude(h => h.Intervenant)
                .Include(x => x.Modules).ThenInclude(m => m.ModuleAcademie)
                .AsSplitQuery()
                .FirstOrDefaultAsync(x => x.Id == id, ct);

            if (c is null)
            {
                return Results.NotFound();
            }

            var parcours = await db.Parcours
                .Where(p => p.CertificationId == id)
                .Select(p => new { p.Etape, p.DateDemande })
                .ToListAsync(ct);

            return Results.Ok(new
            {
                // ---- identité et source ----
                c.Id, c.CodeRncp, c.IdFiche, c.Intitule, c.Abrege,
                lien = c.LienFranceCompetences,
                c.EtatFiche, c.ActifFranceCompetences, c.TypeEnregistrement,
                c.Niveau, c.LibelleNiveau, c.DomaineEi,
                statutInterne = c.StatutInterne.ToString(),
                c.DerniereSynchronisation,

                // ---- dates réglementaires ----
                dates = new
                {
                    c.DateDecision, c.DateFinEnregistrement, c.DateLimiteDelivrance,
                    c.DateDerniereModificationFiche,
                },

                // ---- modalités de VAE ----
                vae = new
                {
                    ouverte = c.VoieVaeOuverte,
                    compositionJury = c.CompositionJuryVae,
                    autresVoies = new
                    {
                        formationInitiale = c.VoieFormationInitiale,
                        formationContinue = c.VoieFormationContinue,
                        apprentissage = c.VoieApprentissage,
                        contratProfessionnalisation = c.VoieContratProfessionnalisation,
                        candidatLibre = c.VoieCandidatLibre,
                    },
                },

                // ---- blocs de compétences ----
                blocs = c.Blocs.OrderBy(b => b.Ordre).Select(b => new
                {
                    b.Code, b.Libelle, b.Competences, b.ModalitesEvaluation,
                }),

                // ---- certificateur et contact ----
                certificateurs = c.Certificateurs.Select(cc => new
                {
                    cc.Certificateur!.Id, cc.Certificateur.Nom, cc.Certificateur.Siret,
                    cc.Certificateur.Etat, cc.EstPrincipal,
                    cc.Certificateur.ContactEmail, cc.Certificateur.ContactTelephone,
                }),
                contact = new
                {
                    nom = c.ContactCertificateurNom,
                    email = c.ContactCertificateurEmail,
                    telephone = c.ContactCertificateurTelephone,
                },

                // ---- réseau habilité ----
                aapCompetents = c.Habilitations
                    .Where(h => h.Intervenant!.Type == TypeIntervenant.ArchitecteAccompagnateurParcours)
                    .Select(h => new { h.Intervenant!.Id, nom = h.Intervenant.NomComplet, h.Niveau }),
                accompagnateurs = c.Habilitations
                    .Where(h => h.Intervenant!.Type == TypeIntervenant.Accompagnateur)
                    .Select(h => new
                    {
                        h.Intervenant!.Id, nom = h.Intervenant.NomComplet, h.Niveau,
                        h.Intervenant.Region, h.Intervenant.InterventionDistanciel,
                    }),
                expertsMetier = c.Habilitations
                    .Where(h => h.Intervenant!.Type == TypeIntervenant.ExpertMetier)
                    .Select(h => new { h.Intervenant!.Id, nom = h.Intervenant.NomComplet }),

                // ---- pédagogie ----
                modules = c.Modules.Select(m => new
                {
                    m.ModuleAcademie!.Id, m.ModuleAcademie.Code, m.ModuleAcademie.Titre,
                    m.ModuleAcademie.Type, m.ModuleAcademie.DureeHeures, m.ModuleAcademie.Url,
                    m.Obligatoire,
                }),

                // ---- interne ----
                c.DureeHabituelleJours, c.Particularites,

                // ---- contenu de la fiche ----
                fiche = new
                {
                    c.ActivitesVisees, c.CapacitesAttestees, c.SecteursActivite,
                    c.TypeEmploiAccessibles, c.ObjectifsContexte, c.Prerequis, c.ReglementationActivites,
                },
                codes = new
                {
                    nsf = Deserialiser(c.CodesNsfJson),
                    formacodes = Deserialiser(c.FormacodesJson),
                    rome = Deserialiser(c.CodesRomeJson),
                },
                statistiques = Deserialiser(c.StatistiquesJson),

                // ---- activité EI Groupe ----
                activite = new
                {
                    demandes = parcours.Count,
                    actifs = parcours.Count(p => p.Etape != EtapeParcours.Cloture && p.Etape != EtapeParcours.Sortie),
                    clotures = parcours.Count(p => p.Etape == EtapeParcours.Cloture),
                    sorties = parcours.Count(p => p.Etape == EtapeParcours.Sortie),
                },
            });
        }).WithSummary("Fiche complète : blocs, modalités de VAE, certificateur, réseau habilité, modules.");

        g.MapPut("/{id:int}/interne", async (
                int id, SaisieCertificationInterne s, VaeDbContext db, CancellationToken ct) =>
        {
            var c = await db.Certifications.Include(x => x.Modules).FirstOrDefaultAsync(x => x.Id == id, ct);
            if (c is null)
            {
                return Results.NotFound();
            }

            c.Abrege = Vide(s.Abrege);
            c.DomaineEi = Vide(s.DomaineEi);
            c.StatutInterne = s.StatutInterne;
            c.DureeHabituelleJours = s.DureeHabituelleJours;
            c.Particularites = Vide(s.Particularites);
            c.ContactCertificateurNom = Vide(s.ContactCertificateurNom);
            c.ContactCertificateurEmail = Vide(s.ContactCertificateurEmail);
            c.ContactCertificateurTelephone = Vide(s.ContactCertificateurTelephone);

            if (s.ModulesAcademie is { } modules)
            {
                var voulus = modules.ToHashSet();
                foreach (var m in c.Modules.Where(m => !voulus.Contains(m.ModuleAcademieId)).ToList())
                {
                    c.Modules.Remove(m);
                    db.CertificationModules.Remove(m);
                }

                var actuels = c.Modules.Select(m => m.ModuleAcademieId).ToHashSet();
                foreach (var m in voulus.Where(m => !actuels.Contains(m)))
                {
                    c.Modules.Add(new CertificationModule { CertificationId = id, ModuleAcademieId = m });
                }
            }

            if (s.ExpertsMetierIds is { } experts)
            {
                foreach (var expertId in experts)
                {
                    var existe = await db.Habilitations.AnyAsync(
                        h => h.IntervenantId == expertId && h.CertificationId == id, ct);

                    if (!existe)
                    {
                        db.Habilitations.Add(new HabilitationIntervenant
                        {
                            IntervenantId = expertId,
                            CertificationId = id,
                            Niveau = "Habilité",
                            DateHabilitation = DateOnly.FromDateTime(DateTime.UtcNow),
                        });
                    }
                }
            }

            await db.SaveChangesAsync(ct);
            return Results.Ok(new { c.Id });
        }).WithSummary("Met à jour les seuls champs internes. Les données France Compétences sont en lecture seule.");

        g.MapPost("/", async (
                SaisieCertificationManuelle s, VaeDbContext db,
                FranceCompetencesImporter importeur, CancellationToken ct) =>
        {
            var chiffres = new string(s.CodeRncp.Where(char.IsDigit).ToArray());
            if (chiffres.Length == 0)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["codeRncp"] = ["Indiquez un code RNCP, par exemple 37275 ou RNCP37275."],
                });
            }

            var code = "RNCP" + chiffres;
            if (await db.Certifications.AnyAsync(c => c.CodeRncp == code, ct))
            {
                return Results.Conflict(new { message = $"{code} est déjà au catalogue." });
            }

            // La fiche est créée avec le minimum ; la synchronisation nocturne la
            // complètera depuis France Compétences. On peut aussi la déclencher
            // immédiatement depuis la console d'intégration.
            var certification = new Certification
            {
                CodeRncp = code,
                Intitule = $"{code} — en attente de synchronisation",
                Abrege = Vide(s.Abrege),
                DomaineEi = Vide(s.DomaineEi),
                StatutInterne = StatutCertificationInterne.Active,
            };

            db.Certifications.Add(certification);
            await db.SaveChangesAsync(ct);

            return Results.Created($"/api/certifications/{certification.Id}", new
            {
                certification.Id,
                certification.CodeRncp,
                lien = certification.LienFranceCompetences,
                message = "Certification ajoutée. Lancez la synchronisation France Compétences pour charger la fiche.",
            });
        }).WithSummary("Ajoute une certification au catalogue par son code RNCP.");

        g.MapGet("/domaines", async (VaeDbContext db, CancellationToken ct) =>
            Results.Ok(new
            {
                disponibles = ClassificationDomaine.Tous,
                utilises = await db.Certifications
                    .Where(c => c.DomaineEi != null)
                    .Select(c => c.DomaineEi!)
                    .Distinct().OrderBy(d => d).ToListAsync(ct),
            }));

        app.MapGet("/api/modules-academie", async (VaeDbContext db, CancellationToken ct) =>
            Results.Ok(await db.ModulesAcademie.AsNoTracking()
                .OrderBy(m => m.Type).ThenBy(m => m.Titre)
                .Select(m => new { m.Id, m.Code, m.Titre, m.Nature, m.Type, m.DureeHeures, m.Url, m.Actif })
                .ToListAsync(ct)))
            .WithTags("Référentiel certifications")
            .WithSummary("Catalogue des modules EI Académie mobilisables.");
    }

    private static string? Vide(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();

    private static JsonElement? Deserialiser(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonDocument.Parse(json).RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
