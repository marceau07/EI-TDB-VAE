using EiVae.Domain;
using EiVae.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EiVae.Infrastructure;

/// <summary>
/// Contexte de persistance. PostgreSQL est la cible de production ; le schéma
/// reste portable (aucun type propriétaire hors <c>jsonb</c>, mappé en texte
/// lorsque le fournisseur ne le prend pas en charge).
/// </summary>
public sealed class VaeDbContext(DbContextOptions<VaeDbContext> options) : DbContext(options)
{
    public DbSet<Candidat> Candidats => Set<Candidat>();
    public DbSet<Parcours> Parcours => Set<Parcours>();
    public DbSet<HistoriqueStatut> HistoriqueStatuts => Set<HistoriqueStatut>();
    public DbSet<Seance> Seances => Set<Seance>();
    public DbSet<Financement> Financements => Set<Financement>();
    public DbSet<Facture> Factures => Set<Facture>();
    public DbSet<PieceDossier> Pieces => Set<PieceDossier>();

    public DbSet<Certification> Certifications => Set<Certification>();
    public DbSet<BlocCompetences> Blocs => Set<BlocCompetences>();
    public DbSet<Certificateur> Certificateurs => Set<Certificateur>();
    public DbSet<CertificationCertificateur> CertificationCertificateurs => Set<CertificationCertificateur>();
    public DbSet<ModuleAcademie> ModulesAcademie => Set<ModuleAcademie>();
    public DbSet<CertificationModule> CertificationModules => Set<CertificationModule>();
    public DbSet<ParcoursModule> ParcoursModules => Set<ParcoursModule>();

    public DbSet<Intervenant> Intervenants => Set<Intervenant>();
    public DbSet<HabilitationIntervenant> Habilitations => Set<HabilitationIntervenant>();

    public DbSet<ProjetCollectif> ProjetsCollectifs => Set<ProjetCollectif>();
    public DbSet<Cohorte> Cohortes => Set<Cohorte>();
    public DbSet<AtelierCollectif> Ateliers => Set<AtelierCollectif>();

    public DbSet<GrilleTarifaire> GrillesTarifaires => Set<GrilleTarifaire>();
    public DbSet<ParametreGestion> Parametres => Set<ParametreGestion>();
    public DbSet<AlerteInstance> Alertes => Set<AlerteInstance>();
    public DbSet<ImportRun> Imports => Set<ImportRun>();
    public DbSet<DemandeWeb> DemandesWeb => Set<DemandeWeb>();
    public DbSet<Utilisateur> Utilisateurs => Set<Utilisateur>();
    public DbSet<RoleAcces> RolesAcces => Set<RoleAcces>();
    public DbSet<RegleAlerte> ReglesAlertes => Set<RegleAlerte>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<JournalAudit> JournalAudit => Set<JournalAudit>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        // ---------------- Candidats et parcours ----------------
        b.Entity<Candidat>(e =>
        {
            e.ToTable("candidats");
            e.Property(x => x.Nom).HasMaxLength(160).IsRequired();
            e.Property(x => x.Prenom).HasMaxLength(160).IsRequired();
            e.Property(x => x.Email).HasMaxLength(320);
            e.Property(x => x.Telephone).HasMaxLength(40);
            e.Property(x => x.Departement).HasMaxLength(3);
            e.Property(x => x.Region).HasMaxLength(80);
            e.Property(x => x.IdentifiantFranceVae).HasMaxLength(64);
            e.HasIndex(x => new { x.Nom, x.Prenom });
            e.HasIndex(x => x.Email);
            e.HasIndex(x => x.IdentifiantFranceVae).IsUnique()
             .HasFilter("\"IdentifiantFranceVae\" IS NOT NULL");
        });

        b.Entity<Parcours>(e =>
        {
            e.ToTable("parcours");
            e.Property(x => x.Etape).HasConversion<int>();
            e.Property(x => x.StatutSecondaire).HasConversion<int>();
            e.Property(x => x.MotifSortie).HasConversion<int>();
            e.Property(x => x.ResultatJury).HasConversion<int>();
            e.Property(x => x.Origine).HasConversion<int>();
            e.Property(x => x.BlocsVisesJson).HasColumnType("jsonb");
            e.Property(x => x.BlocsValidesJson).HasColumnType("jsonb");
            e.Property(x => x.HeuresIndividuelPrescrites).HasPrecision(8, 2);
            e.Property(x => x.HeuresCollectifPrescrites).HasPrecision(8, 2);
            e.Property(x => x.HeuresComplementFormatifPrescrites).HasPrecision(8, 2);
            e.Property(x => x.CandidatureFranceVaeId).HasMaxLength(64);
            e.Property(x => x.CodeAcfSolei).HasMaxLength(32);
            e.Property(x => x.SharePointDossier).HasMaxLength(512);

            e.HasOne(x => x.Candidat).WithMany(c => c.Parcours)
             .HasForeignKey(x => x.CandidatId).OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.Certification).WithMany(c => c.Parcours)
             .HasForeignKey(x => x.CertificationId).OnDelete(DeleteBehavior.SetNull);

            // Un intervenant supprimé ne doit jamais emporter les dossiers qu'il suivait.
            e.HasOne(x => x.Aap).WithMany()
             .HasForeignKey(x => x.AapId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Accompagnateur).WithMany()
             .HasForeignKey(x => x.AccompagnateurId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Gestionnaire).WithMany()
             .HasForeignKey(x => x.GestionnaireId).OnDelete(DeleteBehavior.SetNull);

            e.HasOne(x => x.GrilleTarifaire).WithMany()
             .HasForeignKey(x => x.GrilleTarifaireId).OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Cohorte).WithMany(c => c.Parcours)
             .HasForeignKey(x => x.CohorteId).OnDelete(DeleteBehavior.SetNull);

            e.HasIndex(x => x.Etape);
            e.HasIndex(x => x.DateDernierMouvement);
            e.HasIndex(x => x.DateDebutParcours);
            e.HasIndex(x => x.CandidatureFranceVaeId).IsUnique()
             .HasFilter("\"CandidatureFranceVaeId\" IS NOT NULL");
        });

        b.Entity<HistoriqueStatut>(e =>
        {
            e.ToTable("historique_statuts");
            e.Property(x => x.EtapePrecedente).HasConversion<int?>();
            e.Property(x => x.EtapeNouvelle).HasConversion<int>();
            e.Property(x => x.StatutSecondaire).HasConversion<int>();
            e.HasOne(x => x.Parcours).WithMany(p => p.HistoriqueStatuts)
             .HasForeignKey(x => x.ParcoursId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.ParcoursId, x.SurvenuLe });
        });

        b.Entity<Seance>(e =>
        {
            e.ToTable("seances");
            e.Property(x => x.Nature).HasConversion<int>();
            e.Property(x => x.DureeHeures).HasPrecision(6, 2);
            e.Property(x => x.Modalite).HasMaxLength(40);
            e.HasOne(x => x.Parcours).WithMany(p => p.Seances)
             .HasForeignKey(x => x.ParcoursId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Intervenant).WithMany(i => i.Seances)
             .HasForeignKey(x => x.IntervenantId).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(x => new { x.ParcoursId, x.Date });
        });

        b.Entity<Financement>(e =>
        {
            e.ToTable("financements");
            e.Property(x => x.Dispositif).HasConversion<int>();
            e.Property(x => x.MontantAccorde).HasPrecision(10, 2);
            e.Property(x => x.ResteACharge).HasPrecision(10, 2);
            e.Property(x => x.NumeroPriseEnCharge).HasMaxLength(80);
            e.HasOne(x => x.Parcours).WithMany(p => p.Financements)
             .HasForeignKey(x => x.ParcoursId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Facture>(e =>
        {
            e.ToTable("factures");
            e.Property(x => x.Numero).HasMaxLength(40).IsRequired();
            e.Property(x => x.MontantHt).HasPrecision(10, 2);
            e.Property(x => x.CodeAcf).HasMaxLength(32);
            e.HasOne(x => x.Parcours).WithMany(p => p.Factures)
             .HasForeignKey(x => x.ParcoursId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.Numero).IsUnique();
        });

        b.Entity<PieceDossier>(e =>
        {
            e.ToTable("pieces_dossier");
            e.Property(x => x.ExigibleAPartirDe).HasConversion<int>();
            e.Property(x => x.Type).HasMaxLength(60);
            e.Property(x => x.Libelle).HasMaxLength(200);
            e.HasOne(x => x.Parcours).WithMany(p => p.Pieces)
             .HasForeignKey(x => x.ParcoursId).OnDelete(DeleteBehavior.Cascade);
        });

        // ---------------- Certifications ----------------
        b.Entity<Certification>(e =>
        {
            e.ToTable("certifications");
            e.Property(x => x.CodeRncp).HasMaxLength(20).IsRequired();
            e.Property(x => x.Intitule).HasMaxLength(500).IsRequired();
            e.Property(x => x.Abrege).HasMaxLength(60);
            e.Property(x => x.DomaineEi).HasMaxLength(80);
            e.Property(x => x.StatutInterne).HasConversion<int>();
            e.Property(x => x.CodesNsfJson).HasColumnType("jsonb");
            e.Property(x => x.FormacodesJson).HasColumnType("jsonb");
            e.Property(x => x.CodesRomeJson).HasColumnType("jsonb");
            e.Property(x => x.StatistiquesJson).HasColumnType("jsonb");
            e.Ignore(x => x.LienFranceCompetences);
            e.HasIndex(x => x.CodeRncp).IsUnique();
            e.HasIndex(x => x.DomaineEi);
        });

        b.Entity<BlocCompetences>(e =>
        {
            e.ToTable("blocs_competences");
            e.Property(x => x.Code).HasMaxLength(40).IsRequired();
            e.Property(x => x.Libelle).HasMaxLength(1000).IsRequired();
            e.HasOne(x => x.Certification).WithMany(c => c.Blocs)
             .HasForeignKey(x => x.CertificationId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.CertificationId, x.Code }).IsUnique();
        });

        b.Entity<Certificateur>(e =>
        {
            e.ToTable("certificateurs");
            e.Property(x => x.Nom).HasMaxLength(300).IsRequired();
            e.Property(x => x.Siret).HasMaxLength(20);
            e.HasIndex(x => x.Siret).IsUnique().HasFilter("\"Siret\" IS NOT NULL");
        });

        b.Entity<CertificationCertificateur>(e =>
        {
            e.ToTable("certification_certificateurs");
            e.HasKey(x => new { x.CertificationId, x.CertificateurId });
            e.HasOne(x => x.Certification).WithMany(c => c.Certificateurs)
             .HasForeignKey(x => x.CertificationId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Certificateur).WithMany(c => c.Certifications)
             .HasForeignKey(x => x.CertificateurId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ModuleAcademie>(e =>
        {
            e.ToTable("modules_academie");
            e.Property(x => x.Code).HasMaxLength(40).IsRequired();
            e.Property(x => x.Titre).HasMaxLength(300).IsRequired();
            e.Property(x => x.Type).HasMaxLength(40).IsRequired();
            e.Property(x => x.DureeHeures).HasPrecision(6, 2);
            e.Property(x => x.Nature).HasConversion<int>();
            e.HasIndex(x => x.Code).IsUnique();
            e.HasIndex(x => x.Nature);
        });

        b.Entity<ParcoursModule>(e =>
        {
            e.ToTable("parcours_modules");
            e.HasKey(x => new { x.ParcoursId, x.ModuleAcademieId });
            e.Property(x => x.Statut).HasConversion<int>();
            e.HasOne(x => x.Parcours).WithMany(p => p.Modules)
             .HasForeignKey(x => x.ParcoursId).OnDelete(DeleteBehavior.Cascade);
            // Un module encore prescrit ne se supprime pas : on le désactive.
            e.HasOne(x => x.ModuleAcademie).WithMany(m => m.Parcours)
             .HasForeignKey(x => x.ModuleAcademieId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<CertificationModule>(e =>
        {
            e.ToTable("certification_modules");
            e.HasKey(x => new { x.CertificationId, x.ModuleAcademieId });
            e.HasOne(x => x.Certification).WithMany(c => c.Modules)
             .HasForeignKey(x => x.CertificationId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.ModuleAcademie).WithMany(m => m.Certifications)
             .HasForeignKey(x => x.ModuleAcademieId).OnDelete(DeleteBehavior.Cascade);
        });

        // ---------------- Intervenants ----------------
        b.Entity<Intervenant>(e =>
        {
            e.ToTable("intervenants");
            e.Property(x => x.Nom).HasMaxLength(160).IsRequired();
            e.Property(x => x.Prenom).HasMaxLength(160);
            e.Property(x => x.Type).HasConversion<int>();
            e.Property(x => x.Statut).HasConversion<int>();
            e.Property(x => x.Email).HasMaxLength(320);
            e.Property(x => x.Telephone).HasMaxLength(40);
            e.Property(x => x.TarifHoraire).HasPrecision(8, 2);
            e.Property(x => x.CapaciteHeuresTrimestre).HasPrecision(8, 2);
            e.Property(x => x.Siret).HasMaxLength(20);
            e.Property(x => x.SharePointDossier).HasMaxLength(512);
            e.Ignore(x => x.NomComplet);
            e.Ignore(x => x.EstMobilisable);
            e.HasIndex(x => new { x.Nom, x.Prenom });
            e.HasIndex(x => x.Type);
        });

        b.Entity<HabilitationIntervenant>(e =>
        {
            e.ToTable("habilitations");
            e.Property(x => x.Niveau).HasMaxLength(40).IsRequired();
            e.HasOne(x => x.Intervenant).WithMany(i => i.Habilitations)
             .HasForeignKey(x => x.IntervenantId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Certification).WithMany(c => c.Habilitations)
             .HasForeignKey(x => x.CertificationId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.IntervenantId, x.CertificationId }).IsUnique();
        });

        // ---------------- VAE collective ----------------
        b.Entity<ProjetCollectif>(e =>
        {
            e.ToTable("projets_collectifs");
            e.Property(x => x.Nom).HasMaxLength(200).IsRequired();
            e.Property(x => x.RaisonSociale).HasMaxLength(300).IsRequired();
            e.Property(x => x.Siret).HasMaxLength(20);
            e.Property(x => x.Statut).HasMaxLength(40).IsRequired();
            e.Property(x => x.Dispositif).HasConversion<int>();
            e.Property(x => x.MontantContractualise).HasPrecision(12, 2);
            e.HasOne(x => x.AapReferent).WithMany()
             .HasForeignKey(x => x.AapReferentId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<Cohorte>(e =>
        {
            e.ToTable("cohortes");
            e.Property(x => x.Nom).HasMaxLength(200).IsRequired();
            e.HasOne(x => x.ProjetCollectif).WithMany(p => p.Cohortes)
             .HasForeignKey(x => x.ProjetCollectifId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Certification).WithMany()
             .HasForeignKey(x => x.CertificationId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Animateur).WithMany()
             .HasForeignKey(x => x.AnimateurId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<AtelierCollectif>(e =>
        {
            e.ToTable("ateliers_collectifs");
            e.Property(x => x.Theme).HasMaxLength(300).IsRequired();
            e.Property(x => x.DureeHeures).HasPrecision(6, 2);
            e.HasOne(x => x.Cohorte).WithMany(c => c.Ateliers)
             .HasForeignKey(x => x.CohorteId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Intervenant).WithMany()
             .HasForeignKey(x => x.IntervenantId).OnDelete(DeleteBehavior.SetNull);
        });

        // ---------------- Exploitation ----------------
        b.Entity<GrilleTarifaire>(e =>
        {
            e.ToTable("grilles_tarifaires");
            e.Property(x => x.Code).HasMaxLength(20).IsRequired();
            e.Property(x => x.Libelle).HasMaxLength(200).IsRequired();
            foreach (var p in new[]
                     {
                         nameof(GrilleTarifaire.ForfaitArchitecture),
                         nameof(GrilleTarifaire.TarifHoraireIndividuel),
                         nameof(GrilleTarifaire.TarifHoraireCollectif),
                         nameof(GrilleTarifaire.TarifHoraireComplementFormatif),
                         nameof(GrilleTarifaire.FraisJury),
                         nameof(GrilleTarifaire.PlafondHeuresIndividuel),
                         nameof(GrilleTarifaire.PlafondHeuresCollectif),
                         nameof(GrilleTarifaire.PlafondHeuresComplementFormatif),
                         nameof(GrilleTarifaire.PlafondMontantTotal),
                         nameof(GrilleTarifaire.CoutHoraireIntervenant),
                         nameof(GrilleTarifaire.HeuresArchitectureParDossier),
                         nameof(GrilleTarifaire.CoutHoraireArchitecte),
                     })
            {
                e.Property(p).HasPrecision(10, 2);
            }

            e.HasIndex(x => x.Code).IsUnique();
            e.HasIndex(x => x.DateEffet);
        });

        b.Entity<ParametreGestion>(e =>
        {
            e.ToTable("parametres_gestion");
            e.Property(x => x.Cle).HasMaxLength(80).IsRequired();
            e.Property(x => x.Valeur).HasMaxLength(200).IsRequired();
            e.Property(x => x.Libelle).HasMaxLength(200).IsRequired();
            e.HasIndex(x => x.Cle).IsUnique();
        });

        b.Entity<AlerteInstance>(e =>
        {
            e.ToTable("alertes");
            e.Property(x => x.CodeRegle).HasMaxLength(10).IsRequired();
            e.Property(x => x.Severite).HasConversion<int>();
            e.HasOne(x => x.Parcours).WithMany(p => p.Alertes)
             .HasForeignKey(x => x.ParcoursId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.ParcoursId, x.CodeRegle, x.ResolueLe });
            e.HasIndex(x => x.ResolueLe);
        });

        b.Entity<ImportRun>(e =>
        {
            e.ToTable("imports");
            e.Property(x => x.Source).HasConversion<int>();
            e.Property(x => x.Statut).HasConversion<int>();
            e.Property(x => x.Reference).HasMaxLength(300);
            e.HasIndex(x => new { x.Source, x.DemarreLe });
        });

        b.Entity<DemandeWeb>(e =>
        {
            e.ToTable("demandes_web");
            e.Property(x => x.Nom).HasMaxLength(160).IsRequired();
            e.Property(x => x.Prenom).HasMaxLength(160).IsRequired();
            e.Property(x => x.Statut).HasMaxLength(30).IsRequired();
            e.Property(x => x.PayloadJson).HasColumnType("jsonb");
            e.HasIndex(x => x.Statut);
            e.HasIndex(x => x.RecueLe);
        });

        b.Entity<Utilisateur>(e =>
        {
            e.ToTable("utilisateurs");
            e.Property(x => x.Email).HasMaxLength(320).IsRequired();
            e.Property(x => x.Nom).HasMaxLength(160).IsRequired();
            e.Property(x => x.Prenom).HasMaxLength(160);
            e.Ignore(x => x.NomComplet);
            e.HasOne(x => x.RoleAcces).WithMany(r => r.Utilisateurs)
             .HasForeignKey(x => x.RoleAccesId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Intervenant).WithMany()
             .HasForeignKey(x => x.IntervenantId).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(x => x.Email).IsUnique();
        });

        b.Entity<RoleAcces>(e =>
        {
            e.ToTable("roles_acces");
            e.Property(x => x.Nom).HasMaxLength(120).IsRequired();
            e.Property(x => x.Description).HasMaxLength(500);
            e.Property(x => x.DroitsJson).HasColumnType("jsonb").IsRequired();
            e.Property(x => x.NotificationsJson).HasColumnType("jsonb").IsRequired();
            e.HasIndex(x => x.Nom).IsUnique();
        });

        b.Entity<RegleAlerte>(e =>
        {
            e.ToTable("regles_alertes");
            e.Property(x => x.Code).HasMaxLength(10).IsRequired();
            e.Property(x => x.Libelle).HasMaxLength(200).IsRequired();
            e.Property(x => x.Severite).HasConversion<int>();
            e.Property(x => x.Condition).HasConversion<int>();
            e.Property(x => x.JalonReference).HasMaxLength(60);
            e.Property(x => x.JalonAttendu).HasMaxLength(60);
            e.Property(x => x.EtapeMin).HasConversion<int?>();
            e.Property(x => x.EtapeMax).HasConversion<int?>();
            e.Property(x => x.Acteur).HasConversion<int?>();
            e.Property(x => x.Statut).HasConversion<int?>();
            e.Property(x => x.ActionAttendue).HasMaxLength(300).IsRequired();
            e.Property(x => x.Responsable).HasMaxLength(120);
            e.Property(x => x.Fondement).HasMaxLength(300);
            e.HasIndex(x => x.Code).IsUnique();
        });

        b.Entity<Notification>(e =>
        {
            e.ToTable("notifications");
            e.Property(x => x.Evenement).HasConversion<int>();
            e.Property(x => x.Titre).HasMaxLength(200).IsRequired();
            e.Property(x => x.Message).HasMaxLength(2000).IsRequired();
            e.Property(x => x.DestinataireNom).HasMaxLength(200).IsRequired();
            e.Property(x => x.DestinataireEmail).HasMaxLength(320);
            e.Property(x => x.ErreurEmail).HasMaxLength(1000);
            e.Property(x => x.Lien).HasMaxLength(2000);
            e.Property(x => x.LibelleLien).HasMaxLength(120);
            e.HasOne(x => x.Parcours).WithMany()
             .HasForeignKey(x => x.ParcoursId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.ParcoursId, x.Evenement });
            e.HasIndex(x => x.LueLe);
            e.HasIndex(x => x.CreeLe);
        });

        b.Entity<JournalAudit>(e =>
        {
            e.ToTable("journal_audit");
            e.Property(x => x.Entite).HasMaxLength(80).IsRequired();
            e.Property(x => x.EntiteId).HasMaxLength(40).IsRequired();
            e.Property(x => x.Action).HasMaxLength(40).IsRequired();
            e.HasIndex(x => new { x.Entite, x.EntiteId });
            e.HasIndex(x => x.SurvenuLe);
        });
    }

    /// <summary>
    /// Tient à jour les horodatages de modification et la date de démarrage du
    /// parcours, qui détermine la grille tarifaire applicable.
    /// </summary>
    public override int SaveChanges()
    {
        AvantEnregistrement();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        AvantEnregistrement();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void AvantEnregistrement()
    {
        var maintenant = DateTimeOffset.UtcNow;

        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified))
            {
                continue;
            }

            switch (entry.Entity)
            {
                case Candidat c:
                    c.ModifieLe = maintenant;
                    break;
                case Intervenant i:
                    i.ModifieLe = maintenant;
                    break;
                case Certification cert:
                    cert.ModifieLe = maintenant;
                    break;
                case ProjetCollectif pc:
                    pc.ModifieLe = maintenant;
                    break;
                case Parcours p:
                    p.ModifieLe = maintenant;
                    p.DateDebutParcours ??= p.CalculerDateDebut();
                    break;
            }
        }
    }
}
