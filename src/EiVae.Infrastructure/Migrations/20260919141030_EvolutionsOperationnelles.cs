using System;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using EiVae.Domain;
using EiVae.Domain.Entities;
using EiVae.Domain.Services;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace EiVae.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EvolutionsOperationnelles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Role",
                table: "utilisateurs");

            migrationBuilder.AlterColumn<string>(
                name: "Prenom",
                table: "utilisateurs",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RoleAccesId",
                table: "utilisateurs",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "notifications",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Evenement = table.Column<int>(type: "integer", nullable: false),
                    Titre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ParcoursId = table.Column<int>(type: "integer", nullable: true),
                    IntervenantId = table.Column<int>(type: "integer", nullable: true),
                    UtilisateurId = table.Column<int>(type: "integer", nullable: true),
                    RoleAccesId = table.Column<int>(type: "integer", nullable: true),
                    DestinataireNom = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DestinataireEmail = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    CreeLe = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LueLe = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EmailEnvoyeLe = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TentativesEmail = table.Column<int>(type: "integer", nullable: false),
                    ErreurEmail = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_notifications_parcours_ParcoursId",
                        column: x => x.ParcoursId,
                        principalTable: "parcours",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "regles_alertes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Libelle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Severite = table.Column<int>(type: "integer", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    Condition = table.Column<int>(type: "integer", nullable: false),
                    JalonReference = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    JalonAttendu = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    Seuil = table.Column<int>(type: "integer", nullable: true),
                    SeuilMax = table.Column<int>(type: "integer", nullable: true),
                    JoursOuvres = table.Column<bool>(type: "boolean", nullable: false),
                    EtapeMin = table.Column<int>(type: "integer", nullable: true),
                    EtapeMax = table.Column<int>(type: "integer", nullable: true),
                    Acteur = table.Column<int>(type: "integer", nullable: true),
                    Statut = table.Column<int>(type: "integer", nullable: true),
                    ActionAttendue = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Responsable = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Fondement = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Ordre = table.Column<int>(type: "integer", nullable: false),
                    ModifieLe = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_regles_alertes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "roles_acces",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nom = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Ordre = table.Column<int>(type: "integer", nullable: false),
                    DroitsJson = table.Column<string>(type: "jsonb", nullable: false),
                    NotificationsJson = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_roles_acces", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_utilisateurs_RoleAccesId",
                table: "utilisateurs",
                column: "RoleAccesId");

            migrationBuilder.CreateIndex(
                name: "IX_notifications_CreeLe",
                table: "notifications",
                column: "CreeLe");

            migrationBuilder.CreateIndex(
                name: "IX_notifications_LueLe",
                table: "notifications",
                column: "LueLe");

            migrationBuilder.CreateIndex(
                name: "IX_notifications_ParcoursId_Evenement",
                table: "notifications",
                columns: new[] { "ParcoursId", "Evenement" });

            migrationBuilder.CreateIndex(
                name: "IX_regles_alertes_Code",
                table: "regles_alertes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_roles_acces_Nom",
                table: "roles_acces",
                column: "Nom",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_utilisateurs_roles_acces_RoleAccesId",
                table: "utilisateurs",
                column: "RoleAccesId",
                principalTable: "roles_acces",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            RenumeroterEtapes(migrationBuilder);
            migrationBuilder.Sql(ParametresSql);
            migrationBuilder.Sql(ReglesSql());
            migrationBuilder.Sql(SeuilsRepris);
            migrationBuilder.Sql(RolesSql());
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // L'échange est sa propre réciproque.
            RenumeroterEtapes(migrationBuilder);

            migrationBuilder.DropForeignKey(
                name: "FK_utilisateurs_roles_acces_RoleAccesId",
                table: "utilisateurs");

            migrationBuilder.DropTable(
                name: "notifications");

            migrationBuilder.DropTable(
                name: "regles_alertes");

            migrationBuilder.DropTable(
                name: "roles_acces");

            migrationBuilder.DropIndex(
                name: "IX_utilisateurs_RoleAccesId",
                table: "utilisateurs");

            migrationBuilder.DropColumn(
                name: "RoleAccesId",
                table: "utilisateurs");

            migrationBuilder.AlterColumn<string>(
                name: "Prenom",
                table: "utilisateurs",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(160)",
                oldMaxLength: 160,
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Role",
                table: "utilisateurs",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }
    
        /// <summary>
        /// Financement passe avant Faisabilité (4 ⇄ 5), Recevabilité avant Parcours
        /// validé (6 ⇄ 7). Les valeurs sont stockées : on les échange partout.
        /// </summary>
        private static void RenumeroterEtapes(MigrationBuilder mb)
        {
            foreach (var (table, colonne) in new[]
                     {
                         ("parcours", "Etape"),
                         ("historique_statuts", "EtapePrecedente"),
                         ("historique_statuts", "EtapeNouvelle"),
                         ("pieces_dossier", "ExigibleAPartirDe"),
                     })
            {
                mb.Sql($"""
                    UPDATE {table} SET "{colonne}" = CASE "{colonne}"
                        WHEN 4 THEN 5 WHEN 5 THEN 4 WHEN 6 THEN 7 WHEN 7 THEN 6 END
                    WHERE "{colonne}" IN (4, 5, 6, 7);
                    """);
            }
        }

        /// <summary>
        /// Les paramètres ne sont plus recréés à chaque démarrage : ils sont
        /// supprimables. Ils sont posés ici une fois, sans écraser l'existant.
        /// </summary>
        private const string ParametresSql = """
            INSERT INTO parametres_gestion ("Cle", "Valeur", "Libelle", "Unite", "Categorie", "Description", "ModifieLe")
            VALUES
              ('capacite.aap', '12', 'Capacité par AAP', 'dossiers actifs', 'Charge',
               'Détermine le calcul de charge, la capacité disponible et les alertes de surcharge.', now()),
              ('capacite.accompagnateur', '6', 'Capacité par accompagnateur', 'candidats simultanés', 'Charge',
               'Alimente le score de disponibilité de l''aide à l''affectation.', now()),
              ('capacite.accompagnateur.heures', '60', 'Heures mobilisables par trimestre', 'heures', 'Charge',
               'Volume horaire soutenable par accompagnateur sur un trimestre.', now())
            ON CONFLICT ("Cle") DO NOTHING;
            """;

        /// <summary>Règles livrées, avec les seuils par défaut.</summary>
        private static string ReglesSql()
        {
            static string Texte(string v) => v is null ? "NULL" : "'" + v.Replace("'", "''") + "'";
            static string Entier(int? v) => v?.ToString(CultureInfo.InvariantCulture) ?? "NULL";

            var lignes = MoteurAlertes.ReglesParDefaut().Select(r =>
                $"({Texte(r.Code)}, {Texte(r.Libelle)}, {(int)r.Severite}, true, {(int)r.Condition}, "
                + $"{Texte(r.JalonReference)}, {Texte(r.JalonAttendu)}, {Entier(r.Seuil)}, {Entier(r.SeuilMax)}, "
                + $"{(r.JoursOuvres ? "true" : "false")}, {Entier((int?)r.EtapeMin)}, {Entier((int?)r.EtapeMax)}, "
                + $"{Entier((int?)r.Acteur)}, {Entier((int?)r.Statut)}, {Texte(r.ActionAttendue)}, "
                + $"{Texte(r.Responsable)}, {Texte(r.Fondement)}, {r.Ordre}, now())");

            return """
                INSERT INTO regles_alertes ("Code", "Libelle", "Severite", "Active", "Condition",
                    "JalonReference", "JalonAttendu", "Seuil", "SeuilMax", "JoursOuvres", "EtapeMin", "EtapeMax",
                    "Acteur", "Statut", "ActionAttendue", "Responsable", "Fondement", "Ordre", "ModifieLe")
                VALUES
                """ + string.Join(",\n", lignes) + "\nON CONFLICT (\"Code\") DO NOTHING;";
        }

        /// <summary>
        /// Les seuils réglés jusqu'ici dans les paramètres de gestion passent dans
        /// les règles, qui les portent désormais. Les paramètres devenus sans objet
        /// sont ensuite retirés.
        /// </summary>
        private const string SeuilsRepris = """
            CREATE TEMP TABLE seuils_repris AS
            SELECT "Cle", CASE
                     WHEN "Cle" = 'alerte.taux_consommation' AND "Valeur" ~ '^[0-9]+([.,][0-9]+)?$'
                       THEN round(replace("Valeur", ',', '.')::numeric * 100)::int
                     WHEN "Valeur" ~ '^[0-9]+$' THEN "Valeur"::int
                   END AS v
            FROM parametres_gestion
            WHERE "Cle" LIKE 'alerte.%' OR "Cle" LIKE 'parcours.duree_%';

            UPDATE regles_alertes r SET "Seuil" = s.v
            FROM seuils_repris s
            WHERE s.v IS NOT NULL AND (r."Code", s."Cle") IN (
                ('R01', 'alerte.premier_rdv_jours_ouvres'), ('R03', 'alerte.sans_evolution_jours'),
                ('R04', 'alerte.ralenti_jours'), ('R05', 'alerte.recevabilite_jours'),
                ('R06', 'alerte.jury_jours'), ('R07', 'alerte.jury_proche_jours'),
                ('R08', 'alerte.taux_consommation'), ('R09', 'parcours.duree_critique_jours'),
                ('R10', 'parcours.duree_cible_jours'), ('R11', 'alerte.risque_abandon_jours'));

            UPDATE regles_alertes r SET "SeuilMax" = s.v
            FROM seuils_repris s
            WHERE s.v IS NOT NULL AND (r."Code", s."Cle") IN (
                ('R04', 'alerte.sans_evolution_jours'), ('R10', 'parcours.duree_critique_jours'));

            DROP TABLE seuils_repris;

            DELETE FROM parametres_gestion WHERE "Cle" LIKE 'alerte.%' OR "Cle" LIKE 'parcours.duree_%';
            """;

        /// <summary>Matrice des droits de départ ; la coordination est notifiée des créations, la facturation des prescriptions.</summary>
        private static string RolesSql()
        {
            (string Nom, string Droits, string[] Notifications)[] roles =
            [
                ("Direction", "LLLLCLLC", []),
                ("Responsable VAE", "CCCCCCCC", []),
                ("AAP", "PPLLPPPP", []),
                ("Coordination pédagogique", "LCLCNCLL", [nameof(EvenementNotification.CandidatCree)]),
                ("Administratif & finance", "LLLLCNLL", [nameof(EvenementNotification.ParcoursPrescrit)]),
                ("Accompagnateurs", "PPLNNPNN", []),
                ("Qualité", "LLLLLLCL", []),
                ("Digital learning", "LLLNNCNN", []),
                ("Communication & commercial", "RRLNNNNL", []),
            ];

            var valeurs = roles.Select((r, i) =>
            {
                var droits = JsonSerializer.Serialize(NiveauxAcces.Briques
                    .Select((b, k) => (b, n: r.Droits[k].ToString()))
                    .ToDictionary(x => x.b, x => x.n));
                var notifications = JsonSerializer.Serialize(r.Notifications);
                return $"('{r.Nom.Replace("'", "''")}', {i + 1}, '{droits.Replace("'", "''")}'::jsonb, '{notifications}'::jsonb)";
            });

            return "INSERT INTO roles_acces (\"Nom\", \"Ordre\", \"DroitsJson\", \"NotificationsJson\") VALUES\n"
                   + string.Join(",\n", valeurs) + "\nON CONFLICT (\"Nom\") DO NOTHING;";
        }
}
}
