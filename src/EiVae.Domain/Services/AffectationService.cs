using EiVae.Domain.Entities;

namespace EiVae.Domain.Services;

/// <summary>Charge courante d'un intervenant, telle qu'observée sur les parcours actifs.</summary>
public sealed record ChargeIntervenant(
    int IntervenantId,
    int CandidatsActifs,
    decimal HeuresPrescrites,
    decimal HeuresRealisees,
    int Capacite)
{
    public decimal TauxCharge => Capacite == 0 ? 1m : (decimal)CandidatsActifs / Capacite;
    public int PlacesDisponibles => Math.Max(0, Capacite - CandidatsActifs);
}

/// <summary>Proposition d'affectation, avec le détail de ce qui a produit le score.</summary>
public sealed record PropositionAffectation(
    Intervenant Intervenant,
    int Score,
    IReadOnlyList<string> Motifs,
    ChargeIntervenant Charge);

/// <summary>
/// Aide à l'affectation : croise la certification recherchée, l'expertise, la
/// disponibilité, le territoire et la modalité.
///
/// Le score est indicatif et volontairement lisible : chaque point est justifié
/// par un motif affiché à l'utilisateur. L'outil propose, la coordination décide.
/// </summary>
public sealed class AffectationService
{
    public const int PointsHabilitation = 50;
    public const int PointsExperienceCertification = 15;
    public const int PointsSpecialiteProche = 30;
    public const int PointsDisponibiliteMax = 30;
    public const int PointsTerritoire = 15;
    public const int PointsModalite = 5;

    /// <summary>
    /// Classe les intervenants pour une certification donnée. Les intervenants
    /// non mobilisables sont écartés plutôt que mal notés : un intervenant retiré
    /// du réseau ne doit jamais apparaître comme une option faible.
    /// </summary>
    public IReadOnlyList<PropositionAffectation> Proposer(
        Certification certification,
        IEnumerable<Intervenant> intervenants,
        IReadOnlyDictionary<int, ChargeIntervenant> charges,
        IReadOnlyDictionary<int, int> parcoursParIntervenantEtCertification,
        string? territoireSouhaite = null,
        bool? distancielRequis = null)
    {
        var propositions = new List<PropositionAffectation>();

        foreach (var i in intervenants)
        {
            if (!i.EstMobilisable)
            {
                continue;
            }

            if (i.Type is not (TypeIntervenant.Accompagnateur or TypeIntervenant.ArchitecteAccompagnateurParcours))
            {
                continue;
            }

            var motifs = new List<string>();
            var score = 0;

            var habilitation = i.Habilitations
                .FirstOrDefault(h => h.CertificationId == certification.Id);

            if (habilitation is not null)
            {
                var acquise = habilitation.Niveau.Equals("Habilité", StringComparison.OrdinalIgnoreCase);
                score += acquise ? PointsHabilitation : PointsHabilitation / 2;
                motifs.Add(acquise
                    ? "habilité sur cette certification"
                    : "montée en compétences en cours sur cette certification");
            }
            else if (SpecialiteProche(i.Specialites, certification))
            {
                score += PointsSpecialiteProche;
                motifs.Add("spécialité proche du domaine de la certification");
            }

            if (parcoursParIntervenantEtCertification.TryGetValue(i.Id, out var dejaAccompagnes)
                && dejaAccompagnes > 0)
            {
                score += PointsExperienceCertification;
                motifs.Add($"a déjà accompagné {dejaAccompagnes} candidat{(dejaAccompagnes > 1 ? "s" : "")} sur cette certification");
            }

            var charge = charges.TryGetValue(i.Id, out var c)
                ? c
                : new ChargeIntervenant(i.Id, 0, 0, 0, i.CapaciteCandidats ?? 6);

            var dispo = charge.Capacite == 0
                ? 0m
                : Math.Max(0m, 1m - (decimal)charge.CandidatsActifs / charge.Capacite);

            score += (int)Math.Round(dispo * PointsDisponibiliteMax);
            motifs.Add(charge.CandidatsActifs == 0
                ? "aucun candidat en cours"
                : $"{charge.CandidatsActifs} candidat{(charge.CandidatsActifs > 1 ? "s" : "")} en cours sur {charge.Capacite}");

            if (!string.IsNullOrWhiteSpace(territoireSouhaite)
                && MemeTerritoire(i, territoireSouhaite))
            {
                score += PointsTerritoire;
                motifs.Add("présent sur le territoire");
            }

            if (distancielRequis is true)
            {
                if (i.InterventionDistanciel)
                {
                    score += PointsModalite;
                    motifs.Add("intervient à distance");
                }
                else
                {
                    motifs.Add("n'intervient pas à distance");
                }
            }
            else if (distancielRequis is false)
            {
                if (i.InterventionPresentiel)
                {
                    score += PointsModalite;
                }
                else
                {
                    motifs.Add("n'intervient pas en présentiel");
                }
            }
            else
            {
                score += PointsModalite;
            }

            propositions.Add(new PropositionAffectation(i, score, motifs, charge));
        }

        return propositions
            .OrderByDescending(p => p.Score)
            .ThenBy(p => p.Charge.CandidatsActifs)
            .ThenBy(p => p.Intervenant.Nom, StringComparer.CurrentCulture)
            .ToList();
    }

    private static bool MemeTerritoire(Intervenant i, string territoire)
    {
        var cible = territoire.Trim();
        return (i.Region?.Contains(cible, StringComparison.OrdinalIgnoreCase) ?? false)
               || (i.Territoire?.Contains(cible, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    private static bool SpecialiteProche(string? specialites, Certification certification)
    {
        if (string.IsNullOrWhiteSpace(specialites))
        {
            return false;
        }

        var sp = specialites.ToLowerInvariant();
        var domaine = (certification.DomaineEi ?? string.Empty).ToLowerInvariant();

        // On compare sur des radicaux suffisamment longs pour éviter les faux
        // positifs : « social » ne doit pas rapprocher « médico-social » de
        // « réseaux sociaux ».
        foreach (var mot in domaine.Split([' ', '&', ',', '-', '\''], StringSplitOptions.RemoveEmptyEntries))
        {
            if (mot.Length >= 6 && sp.Contains(mot[..6], StringComparison.Ordinal))
            {
                return true;
            }
        }

        var intitule = certification.Intitule.ToLowerInvariant();
        foreach (var mot in intitule.Split([' ', '\'', '-'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (mot.Length >= 7 && sp.Contains(mot[..7], StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
