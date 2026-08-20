using LPS.APS.BusinessRules.Calculators;
using LPS.APS.BusinessRules.Models;
using LPS.APS.Core.Dto;
using LPS.APS.Core.Enum;

namespace LPS.APS.BusinessRules.Converters;

public class ProcurementFactConverter
{
    private readonly ProcurementEtaCalculator _etaCalculator;

    public ProcurementFactConverter(ProcurementEtaCalculator etaCalculator)
    {
        _etaCalculator = etaCalculator ?? throw new ArgumentNullException(nameof(etaCalculator));
    }

    public TimedSupplyFact ConvertToTimedSupplyFact(RawProcurementFact rawFact)
    {
        if (rawFact == null)
            throw new ArgumentNullException(nameof(rawFact));

        var effectiveEta = _etaCalculator.CalculateEffectiveEta(rawFact);

        return new TimedSupplyFact
        {
            MaterialId = rawFact.MaterialId,
            MaterialCode = rawFact.MaterialCode,
            FactoryId = rawFact.FactoryId,
            FactoryCode = rawFact.FactoryCode,
            AvailableTime = effectiveEta,
            RemainingQty = rawFact.RemainingQty,
            SupplyType = rawFact.SupplyType,
            WarehouseCode = rawFact.StorageCode,
            PhysicalSourceKey = rawFact.PhysicalSourceKey,
            SourceDocumentLineNo = rawFact.SourceDocumentLineNo,
            Commitment = rawFact.Commitment,
            SourceUpdatedAt = rawFact.SourceUpdatedAt
        };
    }

    public IReadOnlyList<TimedSupplyFact> ConvertBatch(IReadOnlyList<RawProcurementFact> rawFacts)
    {
        if (rawFacts == null)
            throw new ArgumentNullException(nameof(rawFacts));

        var results = new List<TimedSupplyFact>(rawFacts.Count);

        foreach (var rawFact in rawFacts)
        {
            try
            {
                var timedFact = ConvertToTimedSupplyFact(rawFact);
                results.Add(timedFact);
            }
            catch (InvalidOperationException ex)
            {
                throw new InvalidOperationException(
                    $"Failed to convert procurement fact {rawFact.PhysicalSourceKey}: {ex.Message}",
                    ex);
            }
        }

        return results;
    }

    private SupplySourceType MapSupplyTypeToEnum(string supplyType)
    {
        return supplyType switch
        {
            "OPEN_PO_REMAINING" => SupplySourceType.PURCHASE_ORDER,
            "PURCHASE_IN_TRANSIT" => SupplySourceType.PURCHASE_ORDER,
            "ARRIVED_NOT_RECEIVED" => SupplySourceType.PURCHASE_ORDER,
            "VMI_ONSITE" => SupplySourceType.PURCHASE_ORDER,
            _ => throw new ArgumentException(
                $"Unknown SupplyType '{supplyType}'. Expected one of: OPEN_PO_REMAINING, PURCHASE_IN_TRANSIT, ARRIVED_NOT_RECEIVED, VMI_ONSITE",
                nameof(supplyType))
        };
    }
}
