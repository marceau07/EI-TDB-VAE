using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EiVae.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RecevabiliteNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LibelleLien",
                table: "notifications",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Lien",
                table: "notifications",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            // La recevabilité validée est notifiée au digital learning (fiche Word)
            // et à la coordination (accompagnateur à attribuer). Rôles modifiables
            // ensuite dans la matrice des droits.
            foreach (var (role, evenement) in new[]
                     {
                         ("Digital learning", "RecevabiliteValidee"),
                         ("Coordination pédagogique", "AccompagnateurAAttribuer"),
                     })
            {
                migrationBuilder.Sql($"""
                    UPDATE roles_acces
                    SET "NotificationsJson" = "NotificationsJson" || '["{evenement}"]'::jsonb
                    WHERE "Nom" = '{role.Replace("'", "''")}'
                      AND NOT ("NotificationsJson" @> '["{evenement}"]'::jsonb);
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LibelleLien",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "Lien",
                table: "notifications");
        }
    }
}
