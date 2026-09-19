using EiVae.Domain.Entities;
using EiVae.Domain.Services;
using EiVae.Infrastructure.Integrations.FranceCompetences;

namespace EiVae.Tests;

public sealed class SharePointLinkBuilderTests
{
    private static SharePointLinkBuilder Builder(string site = "https://eigroupe.sharepoint.com/sites/VAE") =>
        new(new SharePointOptions
        {
            SiteUrl = site,
            Bibliotheque = "Documents partages",
            RacineCandidats = "Candidats",
            GabaritDossierCandidat = "{annee}/{nom} {prenom} - {certification}",
        });

    private static Parcours Dossier(string? certification) => new()
    {
        Id = 42,
        DateDemande = new DateOnly(2026, 3, 4),
        Candidat = new Candidat { Nom = "DUPONT", Prenom = "Camille" },
        Certification = certification is null
            ? null
            : new Certification { CodeRncp = "RNCP37275", Intitule = "Formateur", Abrege = certification },
    };

    [Fact]
    public void Le_chemin_suit_le_gabarit_configure()
    {
        Assert.Equal("Candidats/2026/DUPONT Camille - TP FPA",
            Builder().CheminDossierCandidat(Dossier("TP FPA")));
    }

    [Fact]
    public void Un_jeton_vide_ne_laisse_pas_de_separateur_orphelin()
    {
        // Sans nettoyage, on obtiendrait « DUPONT Camille -  ».
        Assert.Equal("Candidats/2026/DUPONT Camille",
            Builder().CheminDossierCandidat(Dossier(null)));
    }

    [Fact]
    public void Un_chemin_deja_enregistre_est_respecte_tel_quel()
    {
        var p = Dossier("TP FPA");
        p.SharePointDossier = "Archives/2019/Dossier historique";
        Assert.Equal("Archives/2019/Dossier historique", Builder().CheminDossierCandidat(p));
    }

    [Theory]
    [InlineData("Nom/Prénom", "Nom-Prénom")]
    [InlineData("Dupont: le dossier", "Dupont- le dossier")]
    [InlineData("  espaces   multiples  ", "espaces multiples")]
    [InlineData("point final.", "point final")]
    public void Les_caracteres_refuses_par_sharepoint_sont_remplaces(string entree, string attendu)
    {
        Assert.Equal(attendu, SharePointLinkBuilder.Nettoyer(entree));
    }

    [Fact]
    public void Sans_configuration_aucune_url_n_est_produite()
    {
        var builder = Builder(site: string.Empty);
        Assert.False(builder.EstConfigure);
        Assert.Null(builder.UrlDossierCandidat(Dossier("TP FPA")));
    }

    [Fact]
    public void L_url_pointe_sur_le_dossier_et_non_sur_la_racine()
    {
        var url = Builder().UrlDossierCandidat(Dossier("TP FPA"))!;

        Assert.StartsWith("https://eigroupe.sharepoint.com/sites/VAE/", url, StringComparison.Ordinal);
        Assert.Contains("Forms/AllItems.aspx?id=", url, StringComparison.Ordinal);
        Assert.Contains(Uri.EscapeDataString("/sites/VAE/Documents partages/Candidats/2026"), url,
            StringComparison.Ordinal);
    }
}

public sealed class ClassificationDomaineTests
{
    [Theory]
    [InlineData("Educateur de jeunes enfants", "Petite enfance")]
    [InlineData("Accompagnant éducatif petite enfance", "Petite enfance")]
    [InlineData("Educateur spécialisé", "Sanitaire & social")]
    [InlineData("Formateur professionnel d'adultes", "Formation & RH")]
    [InlineData("Technicien en logistique d'entreposage", "Logistique & transport")]
    [InlineData("Agent de propreté et d'hygiène", "Propreté & environnement")]
    [InlineData("Agent de sûreté et de sécurité privée", "Sécurité")]
    [InlineData("Assistant de vie aux familles", "Service à la personne")]
    public void L_intitule_prime_sur_le_code_nsf(string intitule, string attendu)
    {
        Assert.Equal(attendu, ClassificationDomaine.Deduire(intitule, []));
    }

    [Fact]
    public void Le_code_nsf_sert_de_repli_quand_l_intitule_ne_tranche_pas()
    {
        Assert.Equal("Sanitaire & social", ClassificationDomaine.Deduire("Intitulé sans mot-clé", ["332t"]));
    }

    [Fact]
    public void Une_certification_non_classable_tombe_dans_Autres()
    {
        Assert.Equal("Autres", ClassificationDomaine.Deduire("Libellé totalement inédit", ["999"]));
    }

    [Fact]
    public void Petite_enfance_prime_sur_sanitaire_et_social()
    {
        // « éducateur de jeunes enfants » contient « éducateur », qui rattacherait
        // la certification au social si l'ordre des règles n'était pas respecté.
        Assert.Equal("Petite enfance",
            ClassificationDomaine.Deduire("Diplôme d'État d'éducateur de jeunes enfants", ["332"]));
    }
}
