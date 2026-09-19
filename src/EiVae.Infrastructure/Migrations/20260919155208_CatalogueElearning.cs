using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EiVae.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CatalogueElearning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Nature",
                table: "modules_academie",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "parcours_modules",
                columns: table => new
                {
                    ParcoursId = table.Column<int>(type: "integer", nullable: false),
                    ModuleAcademieId = table.Column<int>(type: "integer", nullable: false),
                    Statut = table.Column<int>(type: "integer", nullable: false),
                    DateAttribution = table.Column<DateOnly>(type: "date", nullable: false),
                    DateFin = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_parcours_modules", x => new { x.ParcoursId, x.ModuleAcademieId });
                    table.ForeignKey(
                        name: "FK_parcours_modules_modules_academie_ModuleAcademieId",
                        column: x => x.ModuleAcademieId,
                        principalTable: "modules_academie",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_parcours_modules_parcours_ParcoursId",
                        column: x => x.ParcoursId,
                        principalTable: "parcours",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_modules_academie_Nature",
                table: "modules_academie",
                column: "Nature");

            migrationBuilder.CreateIndex(
                name: "IX_parcours_modules_ModuleAcademieId",
                table: "parcours_modules",
                column: "ModuleAcademieId");

            // Les modules déjà typés « Complément formatif » changent de nature.
            migrationBuilder.Sql("""
                UPDATE modules_academie SET "Nature" = 2
                WHERE lower("Type") LIKE 'compl%ment%formatif%';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "parcours_modules");

            migrationBuilder.DropIndex(
                name: "IX_modules_academie_Nature",
                table: "modules_academie");

            migrationBuilder.DropColumn(
                name: "Nature",
                table: "modules_academie");
        }
    }
}
