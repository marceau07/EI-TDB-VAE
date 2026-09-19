using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace EiVae.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SchemaInitial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "candidats",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nom = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Prenom = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    Telephone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Ville = table.Column<string>(type: "text", nullable: true),
                    CodePostal = table.Column<string>(type: "text", nullable: true),
                    Departement = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    Region = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    DateNaissance = table.Column<DateOnly>(type: "date", nullable: true),
                    IdentifiantFranceVae = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    ConsentementRgpd = table.Column<bool>(type: "boolean", nullable: false),
                    DateConsentementRgpd = table.Column<DateOnly>(type: "date", nullable: true),
                    CreeLe = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ModifieLe = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_candidats", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "certificateurs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Siret = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Nom = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Etat = table.Column<string>(type: "text", nullable: true),
                    ContactEmail = table.Column<string>(type: "text", nullable: true),
                    ContactTelephone = table.Column<string>(type: "text", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_certificateurs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "certifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CodeRncp = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IdFiche = table.Column<string>(type: "text", nullable: true),
                    Intitule = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    EtatFiche = table.Column<string>(type: "text", nullable: true),
                    ActifFranceCompetences = table.Column<bool>(type: "boolean", nullable: false),
                    Niveau = table.Column<int>(type: "integer", nullable: true),
                    LibelleNiveau = table.Column<string>(type: "text", nullable: true),
                    TypeEnregistrement = table.Column<string>(type: "text", nullable: true),
                    DateDecision = table.Column<DateOnly>(type: "date", nullable: true),
                    DateFinEnregistrement = table.Column<DateOnly>(type: "date", nullable: true),
                    DateLimiteDelivrance = table.Column<DateOnly>(type: "date", nullable: true),
                    DateDerniereModificationFiche = table.Column<DateOnly>(type: "date", nullable: true),
                    VoieVaeOuverte = table.Column<bool>(type: "boolean", nullable: false),
                    CompositionJuryVae = table.Column<string>(type: "text", nullable: true),
                    VoieFormationInitiale = table.Column<bool>(type: "boolean", nullable: false),
                    VoieFormationContinue = table.Column<bool>(type: "boolean", nullable: false),
                    VoieApprentissage = table.Column<bool>(type: "boolean", nullable: false),
                    VoieContratProfessionnalisation = table.Column<bool>(type: "boolean", nullable: false),
                    VoieCandidatLibre = table.Column<bool>(type: "boolean", nullable: false),
                    ActivitesVisees = table.Column<string>(type: "text", nullable: true),
                    CapacitesAttestees = table.Column<string>(type: "text", nullable: true),
                    SecteursActivite = table.Column<string>(type: "text", nullable: true),
                    TypeEmploiAccessibles = table.Column<string>(type: "text", nullable: true),
                    ObjectifsContexte = table.Column<string>(type: "text", nullable: true),
                    Prerequis = table.Column<string>(type: "text", nullable: true),
                    ReglementationActivites = table.Column<string>(type: "text", nullable: true),
                    CodesNsfJson = table.Column<string>(type: "jsonb", nullable: true),
                    FormacodesJson = table.Column<string>(type: "jsonb", nullable: true),
                    CodesRomeJson = table.Column<string>(type: "jsonb", nullable: true),
                    StatistiquesJson = table.Column<string>(type: "jsonb", nullable: true),
                    DerniereSynchronisation = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Abrege = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    DomaineEi = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    StatutInterne = table.Column<int>(type: "integer", nullable: false),
                    DureeHabituelleJours = table.Column<int>(type: "integer", nullable: true),
                    Particularites = table.Column<string>(type: "text", nullable: true),
                    ContactCertificateurNom = table.Column<string>(type: "text", nullable: true),
                    ContactCertificateurEmail = table.Column<string>(type: "text", nullable: true),
                    ContactCertificateurTelephone = table.Column<string>(type: "text", nullable: true),
                    CreeLe = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ModifieLe = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_certifications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "demandes_web",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nom = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Prenom = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Email = table.Column<string>(type: "text", nullable: true),
                    Telephone = table.Column<string>(type: "text", nullable: true),
                    CodePostal = table.Column<string>(type: "text", nullable: true),
                    Ville = table.Column<string>(type: "text", nullable: true),
                    CertificationSouhaitee = table.Column<string>(type: "text", nullable: true),
                    CodeRncpDetecte = table.Column<string>(type: "text", nullable: true),
                    Message = table.Column<string>(type: "text", nullable: true),
                    Source = table.Column<string>(type: "text", nullable: true),
                    RecueLe = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Statut = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ParcoursCreeId = table.Column<int>(type: "integer", nullable: true),
                    CommentaireTraitement = table.Column<string>(type: "text", nullable: true),
                    TraiteeLe = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TraiteePar = table.Column<string>(type: "text", nullable: true),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_demandes_web", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "grilles_tarifaires",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Libelle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DateEffet = table.Column<DateOnly>(type: "date", nullable: false),
                    DateFin = table.Column<DateOnly>(type: "date", nullable: true),
                    ForfaitArchitecture = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    TarifHoraireIndividuel = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    TarifHoraireCollectif = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    TarifHoraireComplementFormatif = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    FraisJury = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    PlafondHeuresIndividuel = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    PlafondHeuresCollectif = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    PlafondHeuresComplementFormatif = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    PlafondMontantTotal = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    CoutHoraireIntervenant = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    HeuresArchitectureParDossier = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    CoutHoraireArchitecte = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_grilles_tarifaires", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "imports",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    Statut = table.Column<int>(type: "integer", nullable: false),
                    DemarreLe = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TermineLe = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    NombreLus = table.Column<int>(type: "integer", nullable: false),
                    NombreCrees = table.Column<int>(type: "integer", nullable: false),
                    NombreMisAJour = table.Column<int>(type: "integer", nullable: false),
                    NombreIgnores = table.Column<int>(type: "integer", nullable: false),
                    NombreErreurs = table.Column<int>(type: "integer", nullable: false),
                    Reference = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Journal = table.Column<string>(type: "text", nullable: true),
                    Declencheur = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_imports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "intervenants",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nom = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Prenom = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Statut = table.Column<int>(type: "integer", nullable: false),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    Telephone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Territoire = table.Column<string>(type: "text", nullable: true),
                    Region = table.Column<string>(type: "text", nullable: true),
                    InterventionDistanciel = table.Column<bool>(type: "boolean", nullable: false),
                    InterventionPresentiel = table.Column<bool>(type: "boolean", nullable: false),
                    Specialites = table.Column<string>(type: "text", nullable: true),
                    TarifHoraire = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    CapaciteCandidats = table.Column<int>(type: "integer", nullable: true),
                    CapaciteHeuresTrimestre = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    DateEntreeReseau = table.Column<DateOnly>(type: "date", nullable: true),
                    DateSortieReseau = table.Column<DateOnly>(type: "date", nullable: true),
                    Siret = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    SharePointDossier = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    CreeLe = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ModifieLe = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_intervenants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "journal_audit",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Entite = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    EntiteId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Action = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Champ = table.Column<string>(type: "text", nullable: true),
                    AncienneValeur = table.Column<string>(type: "text", nullable: true),
                    NouvelleValeur = table.Column<string>(type: "text", nullable: true),
                    Auteur = table.Column<string>(type: "text", nullable: true),
                    SurvenuLe = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_journal_audit", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "modules_academie",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Titre = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    DureeHeures = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: true),
                    Url = table.Column<string>(type: "text", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Actif = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_modules_academie", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "parametres_gestion",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Cle = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Valeur = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Libelle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Unite = table.Column<string>(type: "text", nullable: true),
                    Categorie = table.Column<string>(type: "text", nullable: true),
                    ModifieLe = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ModifiePar = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_parametres_gestion", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "blocs_competences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CertificationId = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Libelle = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Competences = table.Column<string>(type: "text", nullable: true),
                    ModalitesEvaluation = table.Column<string>(type: "text", nullable: true),
                    Ordre = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_blocs_competences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_blocs_competences_certifications_CertificationId",
                        column: x => x.CertificationId,
                        principalTable: "certifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "certification_certificateurs",
                columns: table => new
                {
                    CertificationId = table.Column<int>(type: "integer", nullable: false),
                    CertificateurId = table.Column<int>(type: "integer", nullable: false),
                    EstPrincipal = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_certification_certificateurs", x => new { x.CertificationId, x.CertificateurId });
                    table.ForeignKey(
                        name: "FK_certification_certificateurs_certificateurs_CertificateurId",
                        column: x => x.CertificateurId,
                        principalTable: "certificateurs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_certification_certificateurs_certifications_CertificationId",
                        column: x => x.CertificationId,
                        principalTable: "certifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "habilitations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IntervenantId = table.Column<int>(type: "integer", nullable: false),
                    CertificationId = table.Column<int>(type: "integer", nullable: false),
                    Niveau = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    DateHabilitation = table.Column<DateOnly>(type: "date", nullable: true),
                    Commentaire = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_habilitations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_habilitations_certifications_CertificationId",
                        column: x => x.CertificationId,
                        principalTable: "certifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_habilitations_intervenants_IntervenantId",
                        column: x => x.IntervenantId,
                        principalTable: "intervenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "projets_collectifs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nom = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RaisonSociale = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Siret = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    SecteurActivite = table.Column<string>(type: "text", nullable: true),
                    Effectif = table.Column<int>(type: "integer", nullable: true),
                    ConventionCollective = table.Column<string>(type: "text", nullable: true),
                    Opco = table.Column<string>(type: "text", nullable: true),
                    ReferentNom = table.Column<string>(type: "text", nullable: true),
                    ReferentFonction = table.Column<string>(type: "text", nullable: true),
                    ReferentEmail = table.Column<string>(type: "text", nullable: true),
                    ReferentTelephone = table.Column<string>(type: "text", nullable: true),
                    AapReferentId = table.Column<int>(type: "integer", nullable: true),
                    Statut = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    DateDiagnostic = table.Column<DateOnly>(type: "date", nullable: true),
                    DateContractualisation = table.Column<DateOnly>(type: "date", nullable: true),
                    DateOuverture = table.Column<DateOnly>(type: "date", nullable: true),
                    DateCloturePrevue = table.Column<DateOnly>(type: "date", nullable: true),
                    DateCloture = table.Column<DateOnly>(type: "date", nullable: true),
                    Dispositif = table.Column<int>(type: "integer", nullable: false),
                    MontantContractualise = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    ReferenceContrat = table.Column<string>(type: "text", nullable: true),
                    SharePointDossier = table.Column<string>(type: "text", nullable: true),
                    Commentaire = table.Column<string>(type: "text", nullable: true),
                    CreeLe = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ModifieLe = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_projets_collectifs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_projets_collectifs_intervenants_AapReferentId",
                        column: x => x.AapReferentId,
                        principalTable: "intervenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "utilisateurs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    Nom = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Prenom = table.Column<string>(type: "text", nullable: true),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    Actif = table.Column<bool>(type: "boolean", nullable: false),
                    IntervenantId = table.Column<int>(type: "integer", nullable: true),
                    CreeLe = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DerniereConnexion = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_utilisateurs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_utilisateurs_intervenants_IntervenantId",
                        column: x => x.IntervenantId,
                        principalTable: "intervenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "certification_modules",
                columns: table => new
                {
                    CertificationId = table.Column<int>(type: "integer", nullable: false),
                    ModuleAcademieId = table.Column<int>(type: "integer", nullable: false),
                    Obligatoire = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_certification_modules", x => new { x.CertificationId, x.ModuleAcademieId });
                    table.ForeignKey(
                        name: "FK_certification_modules_certifications_CertificationId",
                        column: x => x.CertificationId,
                        principalTable: "certifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_certification_modules_modules_academie_ModuleAcademieId",
                        column: x => x.ModuleAcademieId,
                        principalTable: "modules_academie",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "cohortes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProjetCollectifId = table.Column<int>(type: "integer", nullable: false),
                    Nom = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CertificationId = table.Column<int>(type: "integer", nullable: true),
                    DateOuverture = table.Column<DateOnly>(type: "date", nullable: true),
                    DateCloturePrevue = table.Column<DateOnly>(type: "date", nullable: true),
                    EffectifCible = table.Column<int>(type: "integer", nullable: true),
                    Rythme = table.Column<string>(type: "text", nullable: true),
                    AnimateurId = table.Column<int>(type: "integer", nullable: true),
                    Commentaire = table.Column<string>(type: "text", nullable: true),
                    CreeLe = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cohortes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cohortes_certifications_CertificationId",
                        column: x => x.CertificationId,
                        principalTable: "certifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_cohortes_intervenants_AnimateurId",
                        column: x => x.AnimateurId,
                        principalTable: "intervenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_cohortes_projets_collectifs_ProjetCollectifId",
                        column: x => x.ProjetCollectifId,
                        principalTable: "projets_collectifs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ateliers_collectifs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CohorteId = table.Column<int>(type: "integer", nullable: false),
                    Theme = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    DureeHeures = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    Modalite = table.Column<string>(type: "text", nullable: true),
                    IntervenantId = table.Column<int>(type: "integer", nullable: true),
                    Realise = table.Column<bool>(type: "boolean", nullable: false),
                    UrlEmargement = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ateliers_collectifs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ateliers_collectifs_cohortes_CohorteId",
                        column: x => x.CohorteId,
                        principalTable: "cohortes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ateliers_collectifs_intervenants_IntervenantId",
                        column: x => x.IntervenantId,
                        principalTable: "intervenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "parcours",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CandidatId = table.Column<int>(type: "integer", nullable: false),
                    CertificationId = table.Column<int>(type: "integer", nullable: true),
                    BlocsVisesJson = table.Column<string>(type: "jsonb", nullable: true),
                    Etape = table.Column<int>(type: "integer", nullable: false),
                    StatutSecondaire = table.Column<int>(type: "integer", nullable: false),
                    MotifSortie = table.Column<int>(type: "integer", nullable: false),
                    CommentaireSortie = table.Column<string>(type: "text", nullable: true),
                    Origine = table.Column<int>(type: "integer", nullable: false),
                    AapId = table.Column<int>(type: "integer", nullable: true),
                    AccompagnateurId = table.Column<int>(type: "integer", nullable: true),
                    GestionnaireId = table.Column<int>(type: "integer", nullable: true),
                    DateDemande = table.Column<DateOnly>(type: "date", nullable: true),
                    DatePremierContact = table.Column<DateOnly>(type: "date", nullable: true),
                    DateRecueilBesoins = table.Column<DateOnly>(type: "date", nullable: true),
                    DateRdvFaisabilite = table.Column<DateOnly>(type: "date", nullable: true),
                    DateDepotFaisabilite = table.Column<DateOnly>(type: "date", nullable: true),
                    DateParcoursValide = table.Column<DateOnly>(type: "date", nullable: true),
                    DateRecevabilite = table.Column<DateOnly>(type: "date", nullable: true),
                    DateDebutAccompagnement = table.Column<DateOnly>(type: "date", nullable: true),
                    DateDepotDossierValidation = table.Column<DateOnly>(type: "date", nullable: true),
                    DateJury = table.Column<DateOnly>(type: "date", nullable: true),
                    DateEntretienPostJury = table.Column<DateOnly>(type: "date", nullable: true),
                    DateDebutParcours = table.Column<DateOnly>(type: "date", nullable: true),
                    GrilleTarifaireId = table.Column<int>(type: "integer", nullable: true),
                    HeuresIndividuelPrescrites = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    HeuresCollectifPrescrites = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    HeuresComplementFormatifPrescrites = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    ForfaitArchitectureApplique = table.Column<bool>(type: "boolean", nullable: false),
                    FraisJuryInclus = table.Column<bool>(type: "boolean", nullable: false),
                    ResultatJury = table.Column<int>(type: "integer", nullable: false),
                    BlocsValidesJson = table.Column<string>(type: "jsonb", nullable: true),
                    CommentaireJury = table.Column<string>(type: "text", nullable: true),
                    CandidatureFranceVaeId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CodeAcfSolei = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    EspaceAcademieCree = table.Column<bool>(type: "boolean", nullable: false),
                    UrlEspaceAcademie = table.Column<string>(type: "text", nullable: true),
                    DateDerniereActiviteAcademie = table.Column<DateOnly>(type: "date", nullable: true),
                    SharePointDossier = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    CohorteId = table.Column<int>(type: "integer", nullable: true),
                    DateDernierMouvement = table.Column<DateOnly>(type: "date", nullable: true),
                    Historique = table.Column<string>(type: "text", nullable: true),
                    CreeLe = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ModifieLe = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_parcours", x => x.Id);
                    table.ForeignKey(
                        name: "FK_parcours_candidats_CandidatId",
                        column: x => x.CandidatId,
                        principalTable: "candidats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_parcours_certifications_CertificationId",
                        column: x => x.CertificationId,
                        principalTable: "certifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_parcours_cohortes_CohorteId",
                        column: x => x.CohorteId,
                        principalTable: "cohortes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_parcours_grilles_tarifaires_GrilleTarifaireId",
                        column: x => x.GrilleTarifaireId,
                        principalTable: "grilles_tarifaires",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_parcours_intervenants_AapId",
                        column: x => x.AapId,
                        principalTable: "intervenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_parcours_intervenants_AccompagnateurId",
                        column: x => x.AccompagnateurId,
                        principalTable: "intervenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_parcours_intervenants_GestionnaireId",
                        column: x => x.GestionnaireId,
                        principalTable: "intervenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "alertes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ParcoursId = table.Column<int>(type: "integer", nullable: false),
                    CodeRegle = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Severite = table.Column<int>(type: "integer", nullable: false),
                    Detail = table.Column<string>(type: "text", nullable: true),
                    DetecteeLe = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ResolueLe = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReporteeJusquA = table.Column<DateOnly>(type: "date", nullable: true),
                    MotifReport = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alertes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_alertes_parcours_ParcoursId",
                        column: x => x.ParcoursId,
                        principalTable: "parcours",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "factures",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ParcoursId = table.Column<int>(type: "integer", nullable: false),
                    Numero = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    DateEmission = table.Column<DateOnly>(type: "date", nullable: false),
                    Financeur = table.Column<string>(type: "text", nullable: true),
                    MontantHt = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    DateReglement = table.Column<DateOnly>(type: "date", nullable: true),
                    CodeAcf = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Commentaire = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_factures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_factures_parcours_ParcoursId",
                        column: x => x.ParcoursId,
                        principalTable: "parcours",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "financements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ParcoursId = table.Column<int>(type: "integer", nullable: false),
                    Dispositif = table.Column<int>(type: "integer", nullable: false),
                    Financeur = table.Column<string>(type: "text", nullable: true),
                    NumeroPriseEnCharge = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    MontantAccorde = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    ResteACharge = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    DateDemande = table.Column<DateOnly>(type: "date", nullable: true),
                    DateSecurisation = table.Column<DateOnly>(type: "date", nullable: true),
                    Commentaire = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_financements_parcours_ParcoursId",
                        column: x => x.ParcoursId,
                        principalTable: "parcours",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "historique_statuts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ParcoursId = table.Column<int>(type: "integer", nullable: false),
                    EtapePrecedente = table.Column<int>(type: "integer", nullable: true),
                    EtapeNouvelle = table.Column<int>(type: "integer", nullable: false),
                    StatutSecondaire = table.Column<int>(type: "integer", nullable: false),
                    SurvenuLe = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Auteur = table.Column<string>(type: "text", nullable: true),
                    Commentaire = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_historique_statuts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_historique_statuts_parcours_ParcoursId",
                        column: x => x.ParcoursId,
                        principalTable: "parcours",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "pieces_dossier",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ParcoursId = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Libelle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ExigibleAPartirDe = table.Column<int>(type: "integer", nullable: false),
                    Obligatoire = table.Column<bool>(type: "boolean", nullable: false),
                    Presente = table.Column<bool>(type: "boolean", nullable: false),
                    DateDepot = table.Column<DateOnly>(type: "date", nullable: true),
                    CheminSharePoint = table.Column<string>(type: "text", nullable: true),
                    Commentaire = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pieces_dossier", x => x.Id);
                    table.ForeignKey(
                        name: "FK_pieces_dossier_parcours_ParcoursId",
                        column: x => x.ParcoursId,
                        principalTable: "parcours",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "seances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ParcoursId = table.Column<int>(type: "integer", nullable: false),
                    IntervenantId = table.Column<int>(type: "integer", nullable: true),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Nature = table.Column<int>(type: "integer", nullable: false),
                    DureeHeures = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    Modalite = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Emargee = table.Column<bool>(type: "boolean", nullable: false),
                    UrlEmargement = table.Column<string>(type: "text", nullable: true),
                    Objet = table.Column<string>(type: "text", nullable: true),
                    Commentaire = table.Column<string>(type: "text", nullable: true),
                    Realisee = table.Column<bool>(type: "boolean", nullable: false),
                    CreeLe = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_seances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_seances_intervenants_IntervenantId",
                        column: x => x.IntervenantId,
                        principalTable: "intervenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_seances_parcours_ParcoursId",
                        column: x => x.ParcoursId,
                        principalTable: "parcours",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_alertes_ParcoursId_CodeRegle_ResolueLe",
                table: "alertes",
                columns: new[] { "ParcoursId", "CodeRegle", "ResolueLe" });

            migrationBuilder.CreateIndex(
                name: "IX_alertes_ResolueLe",
                table: "alertes",
                column: "ResolueLe");

            migrationBuilder.CreateIndex(
                name: "IX_ateliers_collectifs_CohorteId",
                table: "ateliers_collectifs",
                column: "CohorteId");

            migrationBuilder.CreateIndex(
                name: "IX_ateliers_collectifs_IntervenantId",
                table: "ateliers_collectifs",
                column: "IntervenantId");

            migrationBuilder.CreateIndex(
                name: "IX_blocs_competences_CertificationId_Code",
                table: "blocs_competences",
                columns: new[] { "CertificationId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_candidats_Email",
                table: "candidats",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_candidats_IdentifiantFranceVae",
                table: "candidats",
                column: "IdentifiantFranceVae",
                unique: true,
                filter: "\"IdentifiantFranceVae\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_candidats_Nom_Prenom",
                table: "candidats",
                columns: new[] { "Nom", "Prenom" });

            migrationBuilder.CreateIndex(
                name: "IX_certificateurs_Siret",
                table: "certificateurs",
                column: "Siret",
                unique: true,
                filter: "\"Siret\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_certification_certificateurs_CertificateurId",
                table: "certification_certificateurs",
                column: "CertificateurId");

            migrationBuilder.CreateIndex(
                name: "IX_certification_modules_ModuleAcademieId",
                table: "certification_modules",
                column: "ModuleAcademieId");

            migrationBuilder.CreateIndex(
                name: "IX_certifications_CodeRncp",
                table: "certifications",
                column: "CodeRncp",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_certifications_DomaineEi",
                table: "certifications",
                column: "DomaineEi");

            migrationBuilder.CreateIndex(
                name: "IX_cohortes_AnimateurId",
                table: "cohortes",
                column: "AnimateurId");

            migrationBuilder.CreateIndex(
                name: "IX_cohortes_CertificationId",
                table: "cohortes",
                column: "CertificationId");

            migrationBuilder.CreateIndex(
                name: "IX_cohortes_ProjetCollectifId",
                table: "cohortes",
                column: "ProjetCollectifId");

            migrationBuilder.CreateIndex(
                name: "IX_demandes_web_RecueLe",
                table: "demandes_web",
                column: "RecueLe");

            migrationBuilder.CreateIndex(
                name: "IX_demandes_web_Statut",
                table: "demandes_web",
                column: "Statut");

            migrationBuilder.CreateIndex(
                name: "IX_factures_Numero",
                table: "factures",
                column: "Numero",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_factures_ParcoursId",
                table: "factures",
                column: "ParcoursId");

            migrationBuilder.CreateIndex(
                name: "IX_financements_ParcoursId",
                table: "financements",
                column: "ParcoursId");

            migrationBuilder.CreateIndex(
                name: "IX_grilles_tarifaires_Code",
                table: "grilles_tarifaires",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_grilles_tarifaires_DateEffet",
                table: "grilles_tarifaires",
                column: "DateEffet");

            migrationBuilder.CreateIndex(
                name: "IX_habilitations_CertificationId",
                table: "habilitations",
                column: "CertificationId");

            migrationBuilder.CreateIndex(
                name: "IX_habilitations_IntervenantId_CertificationId",
                table: "habilitations",
                columns: new[] { "IntervenantId", "CertificationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_historique_statuts_ParcoursId_SurvenuLe",
                table: "historique_statuts",
                columns: new[] { "ParcoursId", "SurvenuLe" });

            migrationBuilder.CreateIndex(
                name: "IX_imports_Source_DemarreLe",
                table: "imports",
                columns: new[] { "Source", "DemarreLe" });

            migrationBuilder.CreateIndex(
                name: "IX_intervenants_Nom_Prenom",
                table: "intervenants",
                columns: new[] { "Nom", "Prenom" });

            migrationBuilder.CreateIndex(
                name: "IX_intervenants_Type",
                table: "intervenants",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_journal_audit_Entite_EntiteId",
                table: "journal_audit",
                columns: new[] { "Entite", "EntiteId" });

            migrationBuilder.CreateIndex(
                name: "IX_journal_audit_SurvenuLe",
                table: "journal_audit",
                column: "SurvenuLe");

            migrationBuilder.CreateIndex(
                name: "IX_modules_academie_Code",
                table: "modules_academie",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_parametres_gestion_Cle",
                table: "parametres_gestion",
                column: "Cle",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_parcours_AapId",
                table: "parcours",
                column: "AapId");

            migrationBuilder.CreateIndex(
                name: "IX_parcours_AccompagnateurId",
                table: "parcours",
                column: "AccompagnateurId");

            migrationBuilder.CreateIndex(
                name: "IX_parcours_CandidatId",
                table: "parcours",
                column: "CandidatId");

            migrationBuilder.CreateIndex(
                name: "IX_parcours_CandidatureFranceVaeId",
                table: "parcours",
                column: "CandidatureFranceVaeId",
                unique: true,
                filter: "\"CandidatureFranceVaeId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_parcours_CertificationId",
                table: "parcours",
                column: "CertificationId");

            migrationBuilder.CreateIndex(
                name: "IX_parcours_CohorteId",
                table: "parcours",
                column: "CohorteId");

            migrationBuilder.CreateIndex(
                name: "IX_parcours_DateDebutParcours",
                table: "parcours",
                column: "DateDebutParcours");

            migrationBuilder.CreateIndex(
                name: "IX_parcours_DateDernierMouvement",
                table: "parcours",
                column: "DateDernierMouvement");

            migrationBuilder.CreateIndex(
                name: "IX_parcours_Etape",
                table: "parcours",
                column: "Etape");

            migrationBuilder.CreateIndex(
                name: "IX_parcours_GestionnaireId",
                table: "parcours",
                column: "GestionnaireId");

            migrationBuilder.CreateIndex(
                name: "IX_parcours_GrilleTarifaireId",
                table: "parcours",
                column: "GrilleTarifaireId");

            migrationBuilder.CreateIndex(
                name: "IX_pieces_dossier_ParcoursId",
                table: "pieces_dossier",
                column: "ParcoursId");

            migrationBuilder.CreateIndex(
                name: "IX_projets_collectifs_AapReferentId",
                table: "projets_collectifs",
                column: "AapReferentId");

            migrationBuilder.CreateIndex(
                name: "IX_seances_IntervenantId",
                table: "seances",
                column: "IntervenantId");

            migrationBuilder.CreateIndex(
                name: "IX_seances_ParcoursId_Date",
                table: "seances",
                columns: new[] { "ParcoursId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_utilisateurs_Email",
                table: "utilisateurs",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_utilisateurs_IntervenantId",
                table: "utilisateurs",
                column: "IntervenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "alertes");

            migrationBuilder.DropTable(
                name: "ateliers_collectifs");

            migrationBuilder.DropTable(
                name: "blocs_competences");

            migrationBuilder.DropTable(
                name: "certification_certificateurs");

            migrationBuilder.DropTable(
                name: "certification_modules");

            migrationBuilder.DropTable(
                name: "demandes_web");

            migrationBuilder.DropTable(
                name: "factures");

            migrationBuilder.DropTable(
                name: "financements");

            migrationBuilder.DropTable(
                name: "habilitations");

            migrationBuilder.DropTable(
                name: "historique_statuts");

            migrationBuilder.DropTable(
                name: "imports");

            migrationBuilder.DropTable(
                name: "journal_audit");

            migrationBuilder.DropTable(
                name: "parametres_gestion");

            migrationBuilder.DropTable(
                name: "pieces_dossier");

            migrationBuilder.DropTable(
                name: "seances");

            migrationBuilder.DropTable(
                name: "utilisateurs");

            migrationBuilder.DropTable(
                name: "certificateurs");

            migrationBuilder.DropTable(
                name: "modules_academie");

            migrationBuilder.DropTable(
                name: "parcours");

            migrationBuilder.DropTable(
                name: "candidats");

            migrationBuilder.DropTable(
                name: "cohortes");

            migrationBuilder.DropTable(
                name: "grilles_tarifaires");

            migrationBuilder.DropTable(
                name: "certifications");

            migrationBuilder.DropTable(
                name: "projets_collectifs");

            migrationBuilder.DropTable(
                name: "intervenants");
        }
    }
}
