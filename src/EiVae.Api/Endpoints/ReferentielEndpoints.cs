using System.Text.Json;
using System.Text.RegularExpressions;
using EiVae.Api.Services;
using EiVae.Domain;
using EiVae.Domain.Entities;
using EiVae.Domain.Services;
using EiVae.Infrastructure;
using EiVae.Infrastructure.Seeding;
using EiVae.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EiVae.Api.Endpoints;

public sealed record SaisieParametre(
    string Valeur, string? Cle = null, string? Libelle = null, string? Unite = null,
    string? Categorie = null, string? Description = null);

public sealed record SaisieRegle(
    string? Code, string? Libelle, SeveriteAlerte Severite, bool Active, TypeCondition Condition,
    string? JalonReference, string? JalonAttendu, int? Seuil, int? SeuilMax, bool JoursOuvres,
    EtapeParcours? EtapeMin, EtapeParcours? EtapeMax, ActeurDossier? Acteur, StatutSecondaire? Statut,
    string? ActionAttendue, string? Responsable, string? Fondement, int? Ordre);

public sealed record SaisieRole(
    string? Nom, string? Description, Dictionary<string, string>? Droits, List<string>? Notifications, int? Ordre);

public sealed record SaisieUtilisateur(
    string Email, string Nom, string? Prenom, int? RoleAccesId, bool Actif, int? IntervenantId);

public sealed record SaisieGrille(
    string Code, string Libelle, DateOnly DateEffet, DateOnly? DateFin,
    decimal ForfaitArchitecture, decimal TarifHoraireIndividuel, decimal TarifHoraireCollectif,
    decimal TarifHoraireComplementFormatif, decimal FraisJury,
    decimal PlafondHeuresIndividuel, decimal PlafondHeuresCollectif,
    decimal PlafondHeuresComplementFormatif, decimal PlafondMontantTotal,
    decimal CoutHoraireIntervenant, decimal HeuresArchitectureParDossier, decimal CoutHoraireArchitecte);

public static partial class ReferentielEndpoints
{
    public static void MapReferentielEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/referentiel").WithTags("Règles de gestion");

        // ---------------------------------------------------------------- grilles
        g.MapGet("/grilles", async (VaeDbContext db, CancellationToken ct) =>
        {
            var grilles = await db.GrillesTarifaires.AsNoTracking().OrderBy(x => x.DateEffet).ToListAsync(ct);
            var usage = await db.Parcours
                .Where(p => p.GrilleTarifaireId != null)
                .GroupBy(p => p.GrilleTarifaireId!.Value)
                .Select(x => new { Id = x.Key, N = x.Count() })
                .ToDictionaryAsync(x => x.Id, x => x.N, ct);

            return Results.Ok(grilles.Select(x => new
            {
                x.Id, x.Code, x.Libelle, x.DateEffet, x.DateFin,
                x.ForfaitArchitecture, x.TarifHoraireIndividuel, x.TarifHoraireCollectif,
                x.TarifHoraireComplementFormatif, x.FraisJury,
                x.PlafondHeuresIndividuel, x.PlafondHeuresCollectif, x.PlafondHeuresComplementFormatif,
                x.PlafondMontantTotal, x.CoutHoraireIntervenant,
                x.HeuresArchitectureParDossier, x.CoutHoraireArchitecte,
                dossiers = usage.GetValueOrDefault(x.Id),
                enVigueur = x.EstApplicableLe(DateOnly.FromDateTime(DateTime.UtcNow)),
            }));
        }).WithSummary("Grilles tarifaires successives et nombre de dossiers rattachés à chacune.");

        g.MapPost("/grilles", async (SaisieGrille s, VaeDbContext db, CancellationToken ct) =>
        {
            if (await db.GrillesTarifaires.AnyAsync(x => x.Code == s.Code, ct))
            {
                return Results.Conflict(new { message = $"La grille « {s.Code} » existe déjà." });
            }

            // La grille précédente est close la veille de la nouvelle, pour qu'il
            // n'existe jamais deux grilles applicables le même jour.
            var precedente = await db.GrillesTarifaires
                .Where(x => x.DateEffet < s.DateEffet)
                .OrderByDescending(x => x.DateEffet).FirstOrDefaultAsync(ct);

            if (precedente is not null && precedente.DateFin is null)
            {
                precedente.DateFin = s.DateEffet.AddDays(-1);
            }

            var grille = new GrilleTarifaire
            {
                Code = s.Code, Libelle = s.Libelle, DateEffet = s.DateEffet, DateFin = s.DateFin,
                ForfaitArchitecture = s.ForfaitArchitecture,
                TarifHoraireIndividuel = s.TarifHoraireIndividuel,
                TarifHoraireCollectif = s.TarifHoraireCollectif,
                TarifHoraireComplementFormatif = s.TarifHoraireComplementFormatif,
                FraisJury = s.FraisJury,
                PlafondHeuresIndividuel = s.PlafondHeuresIndividuel,
                PlafondHeuresCollectif = s.PlafondHeuresCollectif,
                PlafondHeuresComplementFormatif = s.PlafondHeuresComplementFormatif,
                PlafondMontantTotal = s.PlafondMontantTotal,
                CoutHoraireIntervenant = s.CoutHoraireIntervenant,
                HeuresArchitectureParDossier = s.HeuresArchitectureParDossier,
                CoutHoraireArchitecte = s.CoutHoraireArchitecte,
            };

            db.GrillesTarifaires.Add(grille);
            await db.SaveChangesAsync(ct);
            ParametresService.Invalider();

            return Results.Created($"/api/referentiel/grilles/{grille.Id}", new { grille.Id, grille.Code });
        }).WithSummary("Ajoute une grille tarifaire datée. Les parcours en cours conservent la leur.");

        g.MapPost("/grilles/reaffecter", async (
                VaeDbContext db, ParametresService parametres, CancellationToken ct) =>
        {
            // Rattache chaque dossier à la grille correspondant à sa date de
            // démarrage. Utile après une reprise de données ou la correction
            // d'une date : ne touche jamais un dossier déjà facturé.
            var tarification = await parametres.TarificationAsync(ct);

            var parcours = await db.Parcours
                .Include(p => p.Factures)
                .Where(p => p.DateDebutParcours != null)
                .ToListAsync(ct);

            var modifies = 0;
            foreach (var p in parcours.Where(p => p.Factures.Count == 0))
            {
                var attendue = tarification.GrillePour(p.DateDebutParcours!.Value).Id;
                if (p.GrilleTarifaireId != attendue)
                {
                    p.GrilleTarifaireId = attendue;
                    modifies++;
                }
            }

            await db.SaveChangesAsync(ct);
            return Results.Ok(new
            {
                examines = parcours.Count,
                modifies,
                ignoresCarFactures = parcours.Count(p => p.Factures.Count > 0),
            });
        }).WithSummary("Réaffecte les dossiers non facturés à la grille de leur date de démarrage.");

        // ---------------------------------------------------------------- paramètres
        g.MapGet("/parametres", async (VaeDbContext db, CancellationToken ct) =>
            Results.Ok((await db.Parametres.AsNoTracking()
                .OrderBy(p => p.Categorie).ThenBy(p => p.Cle)
                .ToListAsync(ct))
                .Select(p => new
                {
                    p.Id, p.Cle, p.Valeur, p.Libelle, p.Description, p.Unite, p.Categorie, p.ModifieLe,
                    utiliseParApplication = ParametresUtilises.ContainsKey(p.Cle),
                    valeurParDefaut = ParametresUtilises.GetValueOrDefault(p.Cle),
                })))
            .WithSummary("Paramètres de gestion : capacités et valeurs de référence.");

        g.MapPost("/parametres", async (SaisieParametre s, VaeDbContext db, CancellationToken ct) =>
        {
            var cle = s.Cle?.Trim() ?? string.Empty;
            if (!CleValide().IsMatch(cle) || string.IsNullOrWhiteSpace(s.Libelle) || string.IsNullOrWhiteSpace(s.Valeur))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["cle"] = ["Clé (lettres minuscules, chiffres, points, tirets bas), libellé et valeur sont obligatoires."],
                });
            }

            if (await db.Parametres.AnyAsync(p => p.Cle == cle, ct))
            {
                return Results.Conflict(new { message = $"Le paramètre « {cle} » existe déjà." });
            }

            var p = new ParametreGestion
            {
                Cle = cle, Valeur = s.Valeur.Trim(), Libelle = s.Libelle.Trim(),
                Unite = Vide(s.Unite), Categorie = Vide(s.Categorie), Description = Vide(s.Description),
            };
            db.Parametres.Add(p);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/referentiel/parametres/{p.Cle}", new { p.Id, p.Cle });
        }).WithSummary("Ajoute un paramètre de gestion.");

        g.MapPut("/parametres/{cle}", async (string cle, SaisieParametre s, VaeDbContext db, CancellationToken ct) =>
        {
            var p = await db.Parametres.FirstOrDefaultAsync(x => x.Cle == cle, ct);
            if (p is null)
            {
                return Results.NotFound();
            }

            if (string.IsNullOrWhiteSpace(s.Valeur))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["valeur"] = ["La valeur est obligatoire."] });
            }

            p.Valeur = s.Valeur.Trim();
            p.Libelle = string.IsNullOrWhiteSpace(s.Libelle) ? p.Libelle : s.Libelle.Trim();
            p.Unite = s.Unite is null ? p.Unite : Vide(s.Unite);
            p.Categorie = s.Categorie is null ? p.Categorie : Vide(s.Categorie);
            p.Description = s.Description is null ? p.Description : Vide(s.Description);
            p.ModifieLe = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);

            return Results.Ok(new { p.Cle, p.Valeur });
        }).WithSummary("Modifie un paramètre de gestion.");

        g.MapDelete("/parametres/{cle}", async (string cle, VaeDbContext db, CancellationToken ct) =>
        {
            var p = await db.Parametres.FirstOrDefaultAsync(x => x.Cle == cle, ct);
            if (p is null)
            {
                return Results.NotFound();
            }

            db.Parametres.Remove(p);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }).WithSummary("Supprime un paramètre. Un paramètre lu par l'application reprend sa valeur par défaut.");

        // ---------------------------------------------------------------- règles d'alerte
        g.MapGet("/regles", async (VaeDbContext db, CancellationToken ct) =>
        {
            var regles = await db.ReglesAlertes.AsNoTracking()
                .OrderBy(r => r.Ordre).ThenBy(r => r.Code).ToListAsync(ct);
            var ouvertes = await db.Alertes.Where(a => a.ResolueLe == null)
                .GroupBy(a => a.CodeRegle).Select(x => new { x.Key, N = x.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.N, ct);

            return Results.Ok(regles.Select(r => new
            {
                r.Id, r.Code, r.Libelle, r.Active, r.Ordre,
                severite = r.Severite == SeveriteAlerte.Critique ? "critique" : "vigilance",
                severiteCode = r.Severite,
                r.Condition, conditionLibelle = MoteurAlertes.LibelleCondition(r.Condition),
                r.JalonReference, r.JalonAttendu, r.Seuil, r.SeuilMax, r.JoursOuvres,
                r.EtapeMin, r.EtapeMax, r.Acteur, r.Statut,
                seuilDescription = MoteurAlertes.DecrireSeuil(r),
                r.ActionAttendue, r.Responsable, r.Fondement,
                alertesOuvertes = ouvertes.GetValueOrDefault(r.Code),
            }));
        }).WithSummary("Règles d'alerte, actives ou non, avec le nombre d'alertes ouvertes.");

        g.MapGet("/regles/catalogue", () => Results.Ok(new
        {
            conditions = Enum.GetValues<TypeCondition>()
                .Select(c => new { code = c.ToString(), libelle = MoteurAlertes.LibelleCondition(c) }),
            jalons = MoteurAlertes.Jalons.Select(j => new { j.Code, j.Libelle }),
            acteurs = new[]
            {
                new { code = nameof(ActeurDossier.Aap), libelle = "AAP référent" },
                new { code = nameof(ActeurDossier.Accompagnateur), libelle = "Accompagnateur" },
                new { code = nameof(ActeurDossier.Gestionnaire), libelle = "Gestionnaire" },
            },
            statuts = Enum.GetValues<StatutSecondaire>().Where(s => s != StatutSecondaire.Aucun)
                .Select(s => new { code = s.ToString(), libelle = MoteurAlertes.LibelleStatut(s) }),
            etapes = Enum.GetValues<EtapeParcours>().Where(e => e != EtapeParcours.Sortie)
                .Select(e => new { code = e.ToString(), rang = (int)e, libelle = MoteurAlertes.Libelle(e) }),
        })).WithSummary("Valeurs possibles pour composer une règle d'alerte.");

        g.MapPost("/regles", async (
                SaisieRegle s, VaeDbContext db, ServiceAlertes alertes, CancellationToken ct) =>
        {
            var regle = new RegleAlerte { Code = string.Empty, Libelle = string.Empty, ActionAttendue = string.Empty };
            if (Appliquer(regle, s) is { } erreurs)
            {
                return Results.ValidationProblem(erreurs);
            }

            if (await db.ReglesAlertes.AnyAsync(r => r.Code == regle.Code, ct))
            {
                return Results.Conflict(new { message = $"La règle {regle.Code} existe déjà." });
            }

            regle.Ordre = s.Ordre ?? (await db.ReglesAlertes.MaxAsync(r => (int?)r.Ordre, ct) ?? 0) + 1;
            db.ReglesAlertes.Add(regle);
            await db.SaveChangesAsync(ct);

            ParametresService.Invalider();
            var ouvertes = await alertes.RafraichirAsync(ct);
            return Results.Created($"/api/referentiel/regles/{regle.Id}", new { regle.Id, regle.Code, alertesOuvertes = ouvertes });
        }).WithSummary("Crée une règle d'alerte et recalcule aussitôt les alertes.");

        g.MapPut("/regles/{id:int}", async (
                int id, SaisieRegle s, VaeDbContext db, ServiceAlertes alertes, CancellationToken ct) =>
        {
            var regle = await db.ReglesAlertes.FirstOrDefaultAsync(r => r.Id == id, ct);
            if (regle is null)
            {
                return Results.NotFound();
            }

            var ancienCode = regle.Code;
            if (Appliquer(regle, s) is { } erreurs)
            {
                return Results.ValidationProblem(erreurs);
            }

            // Le code identifie les alertes historisées : il ne change pas.
            regle.Code = ancienCode;
            regle.Ordre = s.Ordre ?? regle.Ordre;
            regle.ModifieLe = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);

            ParametresService.Invalider();
            var ouvertes = await alertes.RafraichirAsync(ct);
            return Results.Ok(new { regle.Id, regle.Code, alertesOuvertes = ouvertes });
        }).WithSummary("Modifie une règle d'alerte et recalcule aussitôt les alertes.");

        g.MapDelete("/regles/{id:int}", async (int id, VaeDbContext db, ServiceAlertes alertes, CancellationToken ct) =>
        {
            var regle = await db.ReglesAlertes.FirstOrDefaultAsync(r => r.Id == id, ct);
            if (regle is null)
            {
                return Results.NotFound();
            }

            db.ReglesAlertes.Remove(regle);
            await db.SaveChangesAsync(ct);

            // Les alertes ouvertes au titre de la règle sont closes, pas effacées :
            // l'historique reste consultable.
            ParametresService.Invalider();
            await alertes.RafraichirAsync(ct);
            return Results.NoContent();
        }).WithSummary("Supprime une règle d'alerte ; ses alertes ouvertes sont closes.");

        g.MapGet("/etapes", () => Results.Ok(
                Enum.GetValues<EtapeParcours>().Select(e => new
                {
                    code = e.ToString(),
                    rang = (int)e,
                    libelle = MoteurAlertes.Libelle(e),
                })))
            .WithSummary("Les douze étapes du parcours, dans l'ordre du pipeline.");

        g.MapGet("/nomenclatures", () => Results.Ok(new
        {
            statutsSecondaires = Enum.GetValues<StatutSecondaire>()
                .Where(s => s != StatutSecondaire.Aucun)
                .Select(s => new { code = s.ToString(), libelle = ServiceTableauDeBord.LibelleStatut(s) }),
            motifsSortie = Enum.GetValues<MotifSortie>()
                .Where(m => m != MotifSortie.Aucun)
                .Select(m => new { code = m.ToString(), libelle = ServiceTableauDeBord.LibelleSortie(m) }),
            dispositifs = Enum.GetValues<DispositifFinancement>()
                .Select(d => new { code = d.ToString(), libelle = ServiceTableauDeBord.LibelleDispositif(d) }),
            typesIntervenant = Enum.GetValues<TypeIntervenant>()
                .Select(t => new { code = t.ToString(), libelle = IntervenantsEndpoints.LibelleType(t) }),
            statutsIntervenant = Enum.GetValues<StatutIntervenant>()
                .Select(s => new { code = s.ToString(), libelle = IntervenantsEndpoints.LibelleStatut(s) }),
            naturesHeure = Enum.GetValues<NatureHeure>().Select(n => new { code = n.ToString(), libelle = n.ToString() }),
            origines = Enum.GetValues<OrigineCandidature>().Select(o => new { code = o.ToString(), libelle = o.ToString() }),
            resultatsJury = Enum.GetValues<ResultatJury>().Select(r => new { code = r.ToString(), libelle = r.ToString() }),
        })).WithSummary("Listes de valeurs alimentant les formulaires de saisie.");

        // ---------------------------------------------------------------- SharePoint
        g.MapGet("/sharepoint", (
                IOptions<SharePointOptions> options, SharePointLinkBuilder builder) =>
        {
            var o = options.Value;
            return Results.Ok(new
            {
                configure = builder.EstConfigure,
                o.SiteUrl, o.Bibliotheque, o.RacineCandidats, o.RacineIntervenants, o.RacineProjets,
                o.GabaritDossierCandidat,
                jetons = new[] { "{annee}", "{nom}", "{prenom}", "{parcoursId}", "{certification}", "{acf}" },
                mode = "liens profonds",
            });
        }).WithSummary("Configuration des liens SharePoint.");

        g.MapGet("/sharepoint/apercu", async (
                int? parcoursId, VaeDbContext db, SharePointLinkBuilder builder, CancellationToken ct) =>
        {
            var p = parcoursId is { } id
                ? await db.Parcours.Include(x => x.Candidat).Include(x => x.Certification)
                    .FirstOrDefaultAsync(x => x.Id == id, ct)
                : await db.Parcours.Include(x => x.Candidat).Include(x => x.Certification)
                    .OrderByDescending(x => x.Id).FirstOrDefaultAsync(ct);

            if (p is null)
            {
                return Results.NotFound(new { message = "Aucun dossier disponible pour l'aperçu." });
            }

            return Results.Ok(new
            {
                parcoursId = p.Id,
                candidat = $"{p.Candidat?.Prenom} {p.Candidat?.Nom}".Trim(),
                chemin = builder.CheminDossierCandidat(p),
                url = builder.UrlDossierCandidat(p),
            });
        }).WithSummary("Montre le chemin et l'URL produits pour un dossier réel.");

        // ---------------------------------------------------------------- droits d'accès
        g.MapGet("/droits", async (VaeDbContext db, CancellationToken ct) =>
        {
            var roles = await db.RolesAcces.AsNoTracking().Include(r => r.Utilisateurs)
                .OrderBy(r => r.Ordre).ThenBy(r => r.Nom).ToListAsync(ct);
            var utilisateurs = await db.Utilisateurs.AsNoTracking().Include(u => u.RoleAcces)
                .OrderBy(u => u.Nom).ThenBy(u => u.Prenom).ToListAsync(ct);

            return Results.Ok(new
            {
                briques = NiveauxAcces.Briques,
                legende = NiveauxAcces.Legende,
                evenements = Enum.GetValues<EvenementNotification>()
                    .Where(e => e is not (EvenementNotification.AapAffecte or EvenementNotification.AccompagnateurAffecte))
                    .Select(e => new { code = e.ToString(), libelle = ServiceNotifications.LibelleEvenement(e) }),
                roles = roles.Select(r =>
                {
                    var droits = LireDroits(r.DroitsJson);
                    return new
                    {
                        r.Id, r.Nom, r.Description, r.Ordre,
                        role = r.Nom,
                        droits = NiveauxAcces.Briques.Select(b => droits.GetValueOrDefault(b, "N")),
                        notifications = ServiceNotifications.Evenements(r.NotificationsJson),
                        membres = r.Utilisateurs.Count(u => u.Actif),
                    };
                }),
                utilisateurs = utilisateurs.Select(u => new
                {
                    u.Id, u.Email, u.Nom, u.Prenom, u.NomComplet, u.Actif, u.RoleAccesId, u.IntervenantId,
                    role = u.RoleAcces?.Nom,
                }),
            });
        }).WithSummary("Matrice des droits par rôle et par brique, abonnements aux notifications et utilisateurs.");

        g.MapPost("/roles", async (SaisieRole s, VaeDbContext db, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(s.Nom))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["nom"] = ["Le nom du rôle est obligatoire."] });
            }

            if (await db.RolesAcces.AnyAsync(r => r.Nom == s.Nom.Trim(), ct))
            {
                return Results.Conflict(new { message = $"Le rôle « {s.Nom.Trim()} » existe déjà." });
            }

            var role = new RoleAcces
            {
                Nom = s.Nom.Trim(), Description = Vide(s.Description),
                Ordre = s.Ordre ?? (await db.RolesAcces.MaxAsync(r => (int?)r.Ordre, ct) ?? 0) + 1,
                DroitsJson = EcrireDroits(s.Droits),
                NotificationsJson = EcrireEvenements(s.Notifications),
            };
            db.RolesAcces.Add(role);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/referentiel/roles/{role.Id}", new { role.Id });
        }).WithSummary("Ajoute un rôle à la matrice des droits.");

        g.MapPut("/roles/{id:int}", async (int id, SaisieRole s, VaeDbContext db, CancellationToken ct) =>
        {
            var role = await db.RolesAcces.FirstOrDefaultAsync(r => r.Id == id, ct);
            if (role is null)
            {
                return Results.NotFound();
            }

            if (string.IsNullOrWhiteSpace(s.Nom))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["nom"] = ["Le nom du rôle est obligatoire."] });
            }

            if (await db.RolesAcces.AnyAsync(r => r.Id != id && r.Nom == s.Nom.Trim(), ct))
            {
                return Results.Conflict(new { message = $"Le rôle « {s.Nom.Trim()} » existe déjà." });
            }

            role.Nom = s.Nom.Trim();
            role.Description = Vide(s.Description);
            role.Ordre = s.Ordre ?? role.Ordre;
            if (s.Droits is not null)
            {
                role.DroitsJson = EcrireDroits(s.Droits);
            }

            if (s.Notifications is not null)
            {
                role.NotificationsJson = EcrireEvenements(s.Notifications);
            }

            await db.SaveChangesAsync(ct);
            return Results.Ok(new { role.Id });
        }).WithSummary("Modifie un rôle : nom, droits par brique, notifications.");

        g.MapDelete("/roles/{id:int}", async (int id, VaeDbContext db, CancellationToken ct) =>
        {
            var role = await db.RolesAcces.FirstOrDefaultAsync(r => r.Id == id, ct);
            if (role is null)
            {
                return Results.NotFound();
            }

            db.RolesAcces.Remove(role);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }).WithSummary("Supprime un rôle ; ses utilisateurs restent, sans rôle.");

        g.MapPost("/utilisateurs", async (SaisieUtilisateur s, VaeDbContext db, CancellationToken ct) =>
        {
            if (Verifier(s) is { } erreurs)
            {
                return Results.ValidationProblem(erreurs);
            }

            var email = s.Email.Trim().ToLowerInvariant();
            if (await db.Utilisateurs.AnyAsync(u => u.Email == email, ct))
            {
                return Results.Conflict(new { message = $"Un utilisateur utilise déjà {email}." });
            }

            var u = new Utilisateur
            {
                Email = email, Nom = s.Nom.Trim(), Prenom = Vide(s.Prenom),
                RoleAccesId = s.RoleAccesId, Actif = s.Actif, IntervenantId = s.IntervenantId,
            };
            db.Utilisateurs.Add(u);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/referentiel/utilisateurs/{u.Id}", new { u.Id });
        }).WithSummary("Ajoute un utilisateur et le rattache à un rôle.");

        g.MapPut("/utilisateurs/{id:int}", async (int id, SaisieUtilisateur s, VaeDbContext db, CancellationToken ct) =>
        {
            var u = await db.Utilisateurs.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (u is null)
            {
                return Results.NotFound();
            }

            if (Verifier(s) is { } erreurs)
            {
                return Results.ValidationProblem(erreurs);
            }

            var email = s.Email.Trim().ToLowerInvariant();
            if (await db.Utilisateurs.AnyAsync(x => x.Id != id && x.Email == email, ct))
            {
                return Results.Conflict(new { message = $"Un utilisateur utilise déjà {email}." });
            }

            u.Email = email;
            u.Nom = s.Nom.Trim();
            u.Prenom = Vide(s.Prenom);
            u.RoleAccesId = s.RoleAccesId;
            u.Actif = s.Actif;
            u.IntervenantId = s.IntervenantId;
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { u.Id });
        }).WithSummary("Modifie un utilisateur.");

        g.MapDelete("/utilisateurs/{id:int}", async (int id, VaeDbContext db, CancellationToken ct) =>
        {
            var u = await db.Utilisateurs.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (u is null)
            {
                return Results.NotFound();
            }

            db.Utilisateurs.Remove(u);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }).WithSummary("Supprime un utilisateur.");

        // ---------------------------------------------------------------- reprise de données
        g.MapPost("/amorcer", async (
                ReferentielSeeder referentiel, DonneesInitialesSeeder donnees,
                ServiceAlertes alertes, CancellationToken ct) =>
        {
            await referentiel.AmorcerAsync(ct);
            var resultat = await donnees.ChargerAsync(ct);
            ParametresService.Invalider();
            await alertes.RafraichirAsync(ct);
            return Results.Ok(resultat);
        }).WithSummary("Recharge les référentiels et les données de reprise si la base est vide.");
    }

    /// <summary>Paramètres lus par l'application, avec la valeur appliquée s'ils sont supprimés.</summary>
    private static readonly Dictionary<string, string> ParametresUtilises = new(StringComparer.Ordinal)
    {
        ["capacite.aap"] = "12",
        ["capacite.accompagnateur"] = "6",
    };

    [GeneratedRegex("^[a-z0-9_]+(\\.[a-z0-9_]+)*$")]
    private static partial Regex CleValide();

    [GeneratedRegex("^[A-Za-z0-9_-]{1,10}$")]
    private static partial Regex CodeRegleValide();

    private static string? Vide(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();

    /// <summary>Recopie la saisie dans la règle ; rend les erreurs de cohérence, ou null.</summary>
    private static Dictionary<string, string[]>? Appliquer(RegleAlerte r, SaisieRegle s)
    {
        var erreurs = new Dictionary<string, string[]>();
        void Erreur(string champ, string message) => erreurs[champ] = [message];

        if (string.IsNullOrWhiteSpace(s.Code) || !CodeRegleValide().IsMatch(s.Code.Trim()))
        {
            Erreur("code", "Code de 1 à 10 caractères : lettres, chiffres, tiret.");
        }

        if (string.IsNullOrWhiteSpace(s.Libelle))
        {
            Erreur("libelle", "Le libellé est obligatoire.");
        }

        if (string.IsNullOrWhiteSpace(s.ActionAttendue))
        {
            Erreur("actionAttendue", "L'action attendue est obligatoire.");
        }

        var avecJalon = s.Condition is TypeCondition.DelaiDepuisJalon or TypeCondition.EcheanceProche;
        var avecSeuil = avecJalon || s.Condition == TypeCondition.ConsommationHeures;

        if (avecJalon && MoteurAlertes.Jalon(s.JalonReference) is null)
        {
            Erreur("jalonReference", "Choisissez le jalon de référence.");
        }

        if (s.Condition == TypeCondition.DelaiDepuisJalon && s.JalonAttendu is { Length: > 0 }
            && MoteurAlertes.Jalon(s.JalonAttendu) is null)
        {
            Erreur("jalonAttendu", "Jalon attendu inconnu.");
        }

        if (avecSeuil && s.Seuil is not >= 0)
        {
            Erreur("seuil", "Indiquez un seuil positif.");
        }

        if (s.Condition == TypeCondition.DelaiDepuisJalon && s.SeuilMax is { } max && max <= (s.Seuil ?? 0))
        {
            Erreur("seuilMax", "La borne haute doit dépasser le seuil.");
        }

        if (s.Condition == TypeCondition.ActeurManquant && s.Acteur is null)
        {
            Erreur("acteur", "Choisissez l'acteur attendu.");
        }

        if (s.Condition == TypeCondition.StatutSecondaire && s.Statut is null or StatutSecondaire.Aucun)
        {
            Erreur("statut", "Choisissez le statut surveillé.");
        }

        if (s.EtapeMin is { } a && s.EtapeMax is { } b && a > b)
        {
            Erreur("etapeMax", "L'étape de fin précède l'étape de début.");
        }

        if (erreurs.Count > 0)
        {
            return erreurs;
        }

        r.Code = s.Code!.Trim().ToUpperInvariant();
        r.Libelle = s.Libelle!.Trim();
        r.Severite = s.Severite;
        r.Active = s.Active;
        r.Condition = s.Condition;

        // Les champs sans objet pour la condition choisie sont remis à vide :
        // une règle ne garde jamais un paramètre qui ne s'applique plus.
        r.JalonReference = avecJalon ? s.JalonReference : null;
        r.JalonAttendu = s.Condition == TypeCondition.DelaiDepuisJalon ? Vide(s.JalonAttendu) : null;
        r.Seuil = avecSeuil ? s.Seuil : null;
        r.SeuilMax = s.Condition == TypeCondition.DelaiDepuisJalon ? s.SeuilMax : null;
        r.JoursOuvres = s.Condition == TypeCondition.DelaiDepuisJalon && s.JoursOuvres;
        r.Acteur = s.Condition == TypeCondition.ActeurManquant ? s.Acteur : null;
        r.Statut = s.Condition == TypeCondition.StatutSecondaire ? s.Statut : null;
        r.EtapeMin = s.EtapeMin;
        r.EtapeMax = s.EtapeMax;
        r.ActionAttendue = s.ActionAttendue!.Trim();
        r.Responsable = Vide(s.Responsable);
        r.Fondement = Vide(s.Fondement);
        return null;
    }

    private static Dictionary<string, string[]>? Verifier(SaisieUtilisateur s)
    {
        var erreurs = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(s.Nom))
        {
            erreurs["nom"] = ["Le nom est obligatoire."];
        }

        if (string.IsNullOrWhiteSpace(s.Email) || !s.Email.Contains('@', StringComparison.Ordinal))
        {
            erreurs["email"] = ["Une adresse de courriel valide est obligatoire."];
        }

        return erreurs.Count > 0 ? erreurs : null;
    }

    private static Dictionary<string, string> LireDroits(string? json)
    {
        try
        {
            return string.IsNullOrWhiteSpace(json)
                ? []
                : JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>Ne conserve que les briques connues et les niveaux de la légende.</summary>
    private static string EcrireDroits(Dictionary<string, string>? droits) =>
        JsonSerializer.Serialize(NiveauxAcces.Briques.ToDictionary(
            b => b,
            b => droits?.GetValueOrDefault(b) is { } n && NiveauxAcces.Legende.ContainsKey(n) ? n : "N"));

    private static string EcrireEvenements(IEnumerable<string>? evenements) =>
        JsonSerializer.Serialize((evenements ?? [])
            .Where(e => Enum.TryParse<EvenementNotification>(e, true, out _))
            .Select(e => Enum.Parse<EvenementNotification>(e, true).ToString())
            .Distinct()
            .ToList());
}
