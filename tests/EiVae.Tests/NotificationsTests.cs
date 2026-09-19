using EiVae.Domain;
using EiVae.Infrastructure.Services;

namespace EiVae.Tests;

public sealed class EtatDossierTests
{
    private static EtatDossier Etat(EtapeParcours etape, DateOnly? recevabilite = null, DateOnly? valide = null) =>
        new(null, null, etape, valide, recevabilite);

    [Theory]
    [InlineData(EtapeParcours.Faisabilite, false)]
    [InlineData(EtapeParcours.Recevabilite, true)]
    [InlineData(EtapeParcours.ParcoursValide, true)]
    [InlineData(EtapeParcours.Sortie, false)]
    public void La_recevabilite_est_validee_des_l_etape_E6(EtapeParcours etape, bool attendu)
    {
        Assert.Equal(attendu, Etat(etape).EstRecevable);
    }

    [Fact]
    public void La_date_de_recevabilite_suffit_a_valider_la_recevabilite()
    {
        Assert.True(Etat(EtapeParcours.Faisabilite, recevabilite: new DateOnly(2026, 9, 1)).EstRecevable);
    }

    [Fact]
    public void Un_parcours_recevable_n_est_pas_encore_prescrit()
    {
        var etat = Etat(EtapeParcours.Recevabilite);
        Assert.True(etat.EstRecevable);
        Assert.False(etat.EstPrescrit);
    }
}
