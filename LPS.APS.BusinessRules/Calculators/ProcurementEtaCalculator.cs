using LPS.APS.BusinessRules.Models;

namespace LPS.APS.BusinessRules.Calculators;

public class ProcurementEtaCalculator
{
    public DateTime CalculateEffectiveEta(RawProcurementFact fact)
    {
        if (fact == null)
            throw new ArgumentNullException(nameof(fact));

        if (fact.ManualEta.HasValue)
            return fact.ManualEta.Value;

        if (fact.Eta.HasValue)
            return fact.Eta.Value;

        if (fact.ReleaseDate.HasValue)
            return fact.ReleaseDate.Value;

        throw new InvalidOperationException(
            $"Cannot calculate ETA for procurement fact {fact.PhysicalSourceKey}: " +
            "all three ETA fields (ManualEta, Eta, ReleaseDate) are null. " +
            "ReleaseDate is mandatory per F15 rule.");
    }

    public string GetEtaSource(RawProcurementFact fact)
    {
        if (fact == null)
            throw new ArgumentNullException(nameof(fact));

        if (fact.ManualEta.HasValue)
            return "MANUAL";

        if (fact.Eta.HasValue)
            return "ERP";

        if (fact.ReleaseDate.HasValue)
            return "RELEASE_DATE";

        return "NONE";
    }
}
