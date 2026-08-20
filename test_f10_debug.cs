using LPS.APS.BusinessRules.Calculators;
using LPS.APS.Core.Dto;
using LPS.APS.Core.Enum;
using Microsoft.Extensions.Logging.Abstractions;

var calculator = new ProductionInstructionPositionCalculator(NullLogger<ProductionInstructionPositionCalculator>.Instance);

var input = new ProductionInstructionPositionInput
{
    ProductionInstructionNo = "PI-F10-001",
    MaterialId = 3002,
    FactoryId = 2,
    ErpRemainingQty = 100m,
    TransitFacts = new[]
    {
        new InterplantTransitFact
        {
            TransitDocumentNo = "TRANSIT-002",
            SourceFactoryCode = "CN",
            TargetFactoryCode = "TJ",
            Quantity = 60m,
            SourceDocument = "SH-20260819-002",
            ShippedAt = DateTime.Now.AddDays(-2)
        }
    },
    StrongFacts = new[]
    {
        new ReceivedFact
        {
            DocumentNo = "SH-20260819-002",
            DocumentType = "SH",
            Quantity = 30m,
            ReceivedAt = DateTime.Now,
            WarehouseCode = "TJ-WH01",
            RelatedStageCode = "S10"
        }
    },
    StageProgress = new[]
    {
        new StageProgressFact
        {
            StageCode = "S10",
            StageSequence = 1,
            CumulativeCompletedQty = 30m,
            SnapshotId = 1
        }
    }
};

var results = await calculator.CalculatePositionsAsync(new[] { input });
var result = results.First();

Console.WriteLine($"IsSuccess: {result.IsSuccess}");
Console.WriteLine($"TotalQty: {result.Positions.Sum(p => p.Quantity)}");
Console.WriteLine($"Position Count: {result.Positions.Count}");
foreach (var pos in result.Positions)
{
    Console.WriteLine($"  Type={pos.PositionType}, Stage={pos.StageCode}, Qty={pos.Quantity}");
}
