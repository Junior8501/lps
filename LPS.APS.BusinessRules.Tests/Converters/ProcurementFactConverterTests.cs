using LPS.APS.BusinessRules.Calculators;
using LPS.APS.BusinessRules.Converters;
using LPS.APS.BusinessRules.Models;
using LPS.APS.Core.Enum;
using Moq;
using NUnit.Framework;

namespace LPS.APS.BusinessRules.Tests.Converters;

[TestFixture]
public class ProcurementFactConverterTests
{
    private Mock<ProcurementEtaCalculator> _mockEtaCalculator;
    private ProcurementFactConverter _converter;

    [SetUp]
    public void SetUp()
    {
        _mockEtaCalculator = new Mock<ProcurementEtaCalculator>();
        _converter = new ProcurementFactConverter(_mockEtaCalculator.Object);
    }

    [Test]
    public void ConvertToTimedSupplyFact_WithValidData_ReturnsTimedSupplyFact()
    {
        var rawFact = new RawProcurementFact
        {
            MaterialCode = "MAT001",
            MaterialId = 1001,
            FactoryId = 5001,
            FactoryCode = "FAC01",
            RemainingQty = 100,
            ManualEta = new DateTime(2026, 8, 25),
            ErpEta = new DateTime(2026, 8, 26),
            ReleaseDate = new DateTime(2026, 8, 15),
            WarehouseCode = "WH01",
            SupplyType = "OPEN_PO_REMAINING",
            Commitment = "COMMITTED",
            Confidence = "HIGH",
            PhysicalSourceKey = "PO-20260815-001",
            SourceDocumentLineNo = "10",
            SourceUpdatedAt = new DateTime(2026, 8, 15, 10, 30, 0)
        };

        var expectedEta = new DateTime(2026, 8, 25);
        _mockEtaCalculator.Setup(c => c.CalculateEffectiveEta(rawFact)).Returns(expectedEta);
        _mockEtaCalculator.Setup(c => c.GetEtaSource(rawFact)).Returns("MANUAL");

        var result = _converter.ConvertToTimedSupplyFact(rawFact);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.MaterialCode, Is.EqualTo("MAT001"));
        Assert.That(result.MaterialId, Is.EqualTo(1001));
        Assert.That(result.FactoryId, Is.EqualTo(5001));
        Assert.That(result.FactoryCode, Is.EqualTo("FAC01"));
        Assert.That(result.AvailableTime, Is.EqualTo(expectedEta));
        Assert.That(result.Quantity, Is.EqualTo(100));
        Assert.That(result.SupplySourceType, Is.EqualTo(SupplySourceType.PURCHASE_ORDER));
        Assert.That(result.WarehouseCode, Is.EqualTo("WH01"));
        Assert.That(result.PhysicalSourceKey, Is.EqualTo("PO-20260815-001"));
        Assert.That(result.SourceDocumentLineNo, Is.EqualTo("10"));
        Assert.That(result.Commitment, Is.EqualTo("COMMITTED"));
        Assert.That(result.Confidence, Is.EqualTo("HIGH"));
        Assert.That(result.EtaSource, Is.EqualTo("MANUAL"));
        Assert.That(result.SourceUpdatedAt, Is.EqualTo(new DateTime(2026, 8, 15, 10, 30, 0)));
    }

    [Test]
    [TestCase("OPEN_PO_REMAINING")]
    [TestCase("PURCHASE_IN_TRANSIT")]
    [TestCase("ARRIVED_NOT_RECEIVED")]
    [TestCase("VMI_ONSITE")]
    public void ConvertToTimedSupplyFact_WithAllValidSupplyTypes_MapsToPurchaseOrder(string supplyType)
    {
        var rawFact = new RawProcurementFact
        {
            MaterialId = 1001,
            FactoryId = 5001,
            RemainingQty = 100,
            SupplyType = supplyType,
            PhysicalSourceKey = "PO-001"
        };

        _mockEtaCalculator.Setup(c => c.CalculateEffectiveEta(rawFact)).Returns(DateTime.Now);
        _mockEtaCalculator.Setup(c => c.GetEtaSource(rawFact)).Returns("MANUAL");

        var result = _converter.ConvertToTimedSupplyFact(rawFact);

        Assert.That(result.SupplySourceType, Is.EqualTo(SupplySourceType.PURCHASE_ORDER));
    }

    [Test]
    public void ConvertToTimedSupplyFact_WithInvalidSupplyType_ThrowsArgumentException()
    {
        var rawFact = new RawProcurementFact
        {
            MaterialId = 1001,
            FactoryId = 5001,
            RemainingQty = 100,
            SupplyType = "INVALID_TYPE",
            PhysicalSourceKey = "PO-001"
        };

        _mockEtaCalculator.Setup(c => c.CalculateEffectiveEta(rawFact)).Returns(DateTime.Now);
        _mockEtaCalculator.Setup(c => c.GetEtaSource(rawFact)).Returns("MANUAL");

        var ex = Assert.Throws<ArgumentException>(() => _converter.ConvertToTimedSupplyFact(rawFact));
        Assert.That(ex.Message, Does.Contain("INVALID_TYPE"));
        Assert.That(ex.Message, Does.Contain("OPEN_PO_REMAINING"));
    }

    [Test]
    public void ConvertToTimedSupplyFact_WithNullRawFact_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _converter.ConvertToTimedSupplyFact(null));
    }

    [Test]
    public void ConvertToTimedSupplyFact_WhenEtaCalculationFails_ThrowsInvalidOperationException()
    {
        var rawFact = new RawProcurementFact
        {
            MaterialId = 1001,
            FactoryId = 5001,
            RemainingQty = 100,
            SupplyType = "OPEN_PO_REMAINING",
            PhysicalSourceKey = "PO-001",
            ManualEta = null,
            ErpEta = null,
            ReleaseDate = null
        };

        _mockEtaCalculator.Setup(c => c.CalculateEffectiveEta(rawFact))
            .Throws(new InvalidOperationException("F15 violation"));

        Assert.Throws<InvalidOperationException>(() => _converter.ConvertToTimedSupplyFact(rawFact));
    }

    [Test]
    public void ConvertBatch_WithValidFacts_ReturnsAllConvertedFacts()
    {
        var rawFacts = new List<RawProcurementFact>
        {
            new RawProcurementFact
            {
                MaterialId = 1001,
                FactoryId = 5001,
                RemainingQty = 100,
                SupplyType = "OPEN_PO_REMAINING",
                PhysicalSourceKey = "PO-001"
            },
            new RawProcurementFact
            {
                MaterialId = 1002,
                FactoryId = 5002,
                RemainingQty = 50,
                SupplyType = "VMI_ONSITE",
                PhysicalSourceKey = "VMI-001"
            }
        };

        _mockEtaCalculator.Setup(c => c.CalculateEffectiveEta(It.IsAny<RawProcurementFact>()))
            .Returns(DateTime.Now);
        _mockEtaCalculator.Setup(c => c.GetEtaSource(It.IsAny<RawProcurementFact>()))
            .Returns("MANUAL");

        var result = _converter.ConvertBatch(rawFacts);

        Assert.That(result.Count, Is.EqualTo(2));
        Assert.That(result[0].MaterialId, Is.EqualTo(1001));
        Assert.That(result[0].Quantity, Is.EqualTo(100));
        Assert.That(result[1].MaterialId, Is.EqualTo(1002));
        Assert.That(result[1].Quantity, Is.EqualTo(50));
    }

    [Test]
    public void ConvertBatch_WithEmptyList_ReturnsEmptyList()
    {
        var rawFacts = new List<RawProcurementFact>();

        var result = _converter.ConvertBatch(rawFacts);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Count, Is.EqualTo(0));
    }

    [Test]
    public void ConvertBatch_WithNullList_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _converter.ConvertBatch(null));
    }

    [Test]
    public void ConvertBatch_WhenOneFails_ThrowsWithDetailedMessage()
    {
        var rawFacts = new List<RawProcurementFact>
        {
            new RawProcurementFact
            {
                MaterialId = 1001,
                FactoryId = 5001,
                RemainingQty = 100,
                SupplyType = "OPEN_PO_REMAINING",
                PhysicalSourceKey = "PO-001"
            },
            new RawProcurementFact
            {
                MaterialId = 1002,
                FactoryId = 5002,
                RemainingQty = 50,
                SupplyType = "OPEN_PO_REMAINING",
                PhysicalSourceKey = "PO-002",
                ManualEta = null,
                ErpEta = null,
                ReleaseDate = null
            }
        };

        _mockEtaCalculator.Setup(c => c.CalculateEffectiveEta(rawFacts[0]))
            .Returns(DateTime.Now);
        _mockEtaCalculator.Setup(c => c.GetEtaSource(rawFacts[0]))
            .Returns("MANUAL");
        _mockEtaCalculator.Setup(c => c.CalculateEffectiveEta(rawFacts[1]))
            .Throws(new InvalidOperationException("F15 violation"));

        var ex = Assert.Throws<InvalidOperationException>(() => _converter.ConvertBatch(rawFacts));
        Assert.That(ex.Message, Does.Contain("PO-002"));
        Assert.That(ex.Message, Does.Contain("Failed to convert"));
    }

    [Test]
    public void Constructor_WithNullEtaCalculator_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new ProcurementFactConverter(null));
    }

    [Test]
    public void ConvertToTimedSupplyFact_PreservesAllSourceFields()
    {
        var sourceTime = new DateTime(2026, 8, 15, 10, 30, 0);
        var rawFact = new RawProcurementFact
        {
            MaterialCode = "MAT-SPECIAL",
            MaterialId = 9999,
            FactoryId = 8888,
            FactoryCode = "FAC-99",
            RemainingQty = 250.5m,
            WarehouseCode = "WH-SPECIAL",
            SupplyType = "VMI_ONSITE",
            Commitment = "TENTATIVE",
            Confidence = "MEDIUM",
            PhysicalSourceKey = "VMI-SPECIAL-001",
            SourceDocumentLineNo = "999",
            SourceUpdatedAt = sourceTime
        };

        var expectedEta = new DateTime(2026, 9, 1);
        _mockEtaCalculator.Setup(c => c.CalculateEffectiveEta(rawFact)).Returns(expectedEta);
        _mockEtaCalculator.Setup(c => c.GetEtaSource(rawFact)).Returns("ERP");

        var result = _converter.ConvertToTimedSupplyFact(rawFact);

        Assert.That(result.MaterialCode, Is.EqualTo("MAT-SPECIAL"));
        Assert.That(result.MaterialId, Is.EqualTo(9999));
        Assert.That(result.FactoryId, Is.EqualTo(8888));
        Assert.That(result.FactoryCode, Is.EqualTo("FAC-99"));
        Assert.That(result.Quantity, Is.EqualTo(250.5m));
        Assert.That(result.WarehouseCode, Is.EqualTo("WH-SPECIAL"));
        Assert.That(result.Commitment, Is.EqualTo("TENTATIVE"));
        Assert.That(result.Confidence, Is.EqualTo("MEDIUM"));
        Assert.That(result.PhysicalSourceKey, Is.EqualTo("VMI-SPECIAL-001"));
        Assert.That(result.SourceDocumentLineNo, Is.EqualTo("999"));
        Assert.That(result.SourceUpdatedAt, Is.EqualTo(sourceTime));
        Assert.That(result.EtaSource, Is.EqualTo("ERP"));
        Assert.That(result.AvailableTime, Is.EqualTo(expectedEta));
    }
}
