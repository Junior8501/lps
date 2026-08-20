using LPS.APS.BusinessRules.Calculators;
using LPS.APS.BusinessRules.Models;
using NUnit.Framework;

namespace LPS.APS.BusinessRules.Tests.Calculators;

[TestFixture]
public class ProcurementEtaCalculatorTests
{
    private ProcurementEtaCalculator _calculator;

    [SetUp]
    public void SetUp()
    {
        _calculator = new ProcurementEtaCalculator();
    }

    [Test]
    public void CalculateEffectiveEta_WithManualEta_ReturnsManualEta()
    {
        var fact = new RawProcurementFact
        {
            ManualEta = new DateTime(2026, 8, 25),
            ErpEta = new DateTime(2026, 8, 26),
            ReleaseDate = new DateTime(2026, 8, 15),
            PhysicalSourceKey = "PO-001"
        };

        var result = _calculator.CalculateEffectiveEta(fact);

        Assert.That(result, Is.EqualTo(new DateTime(2026, 8, 25)));
    }

    [Test]
    public void CalculateEffectiveEta_WithoutManualEta_ReturnsErpEta()
    {
        var fact = new RawProcurementFact
        {
            ManualEta = null,
            ErpEta = new DateTime(2026, 8, 26),
            ReleaseDate = new DateTime(2026, 8, 15),
            PhysicalSourceKey = "PO-002"
        };

        var result = _calculator.CalculateEffectiveEta(fact);

        Assert.That(result, Is.EqualTo(new DateTime(2026, 8, 26)));
    }

    [Test]
    public void CalculateEffectiveEta_WithOnlyReleaseDate_ReturnsReleaseDate()
    {
        var fact = new RawProcurementFact
        {
            ManualEta = null,
            ErpEta = null,
            ReleaseDate = new DateTime(2026, 8, 15),
            PhysicalSourceKey = "PO-003"
        };

        var result = _calculator.CalculateEffectiveEta(fact);

        Assert.That(result, Is.EqualTo(new DateTime(2026, 8, 15)));
    }

    [Test]
    public void CalculateEffectiveEta_WithAllNull_ThrowsInvalidOperationException()
    {
        var fact = new RawProcurementFact
        {
            ManualEta = null,
            ErpEta = null,
            ReleaseDate = null,
            PhysicalSourceKey = "PO-004"
        };

        var ex = Assert.Throws<InvalidOperationException>(() => _calculator.CalculateEffectiveEta(fact));
        Assert.That(ex.Message, Does.Contain("PO-004"));
        Assert.That(ex.Message, Does.Contain("F15"));
    }

    [Test]
    public void CalculateEffectiveEta_WithNullFact_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _calculator.CalculateEffectiveEta(null));
    }

    [Test]
    public void GetEtaSource_WithManualEta_ReturnsManual()
    {
        var fact = new RawProcurementFact
        {
            ManualEta = new DateTime(2026, 8, 25),
            ErpEta = new DateTime(2026, 8, 26),
            ReleaseDate = new DateTime(2026, 8, 15)
        };

        var result = _calculator.GetEtaSource(fact);

        Assert.That(result, Is.EqualTo("MANUAL"));
    }

    [Test]
    public void GetEtaSource_WithoutManualEta_ReturnsErp()
    {
        var fact = new RawProcurementFact
        {
            ManualEta = null,
            ErpEta = new DateTime(2026, 8, 26),
            ReleaseDate = new DateTime(2026, 8, 15)
        };

        var result = _calculator.GetEtaSource(fact);

        Assert.That(result, Is.EqualTo("ERP"));
    }

    [Test]
    public void GetEtaSource_WithOnlyReleaseDate_ReturnsReleaseDate()
    {
        var fact = new RawProcurementFact
        {
            ManualEta = null,
            ErpEta = null,
            ReleaseDate = new DateTime(2026, 8, 15)
        };

        var result = _calculator.GetEtaSource(fact);

        Assert.That(result, Is.EqualTo("RELEASE_DATE"));
    }

    [Test]
    public void GetEtaSource_WithAllNull_ReturnsNone()
    {
        var fact = new RawProcurementFact
        {
            ManualEta = null,
            ErpEta = null,
            ReleaseDate = null
        };

        var result = _calculator.GetEtaSource(fact);

        Assert.That(result, Is.EqualTo("NONE"));
    }

    [Test]
    public void GetEtaSource_WithNullFact_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _calculator.GetEtaSource(null));
    }

    [Test]
    public void EtaPriorityOrder_VerifyManualTakesPrecedence()
    {
        var manualDate = new DateTime(2026, 9, 1);
        var erpDate = new DateTime(2026, 8, 20);
        var releaseDate = new DateTime(2026, 8, 10);

        var fact = new RawProcurementFact
        {
            ManualEta = manualDate,
            ErpEta = erpDate,
            ReleaseDate = releaseDate,
            PhysicalSourceKey = "PO-005"
        };

        var effectiveEta = _calculator.CalculateEffectiveEta(fact);
        var source = _calculator.GetEtaSource(fact);

        Assert.That(effectiveEta, Is.EqualTo(manualDate));
        Assert.That(source, Is.EqualTo("MANUAL"));
    }

    [Test]
    public void EtaPriorityOrder_VerifyErpTakesPrecedenceOverReleaseDate()
    {
        var erpDate = new DateTime(2026, 8, 25);
        var releaseDate = new DateTime(2026, 8, 10);

        var fact = new RawProcurementFact
        {
            ManualEta = null,
            ErpEta = erpDate,
            ReleaseDate = releaseDate,
            PhysicalSourceKey = "PO-006"
        };

        var effectiveEta = _calculator.CalculateEffectiveEta(fact);
        var source = _calculator.GetEtaSource(fact);

        Assert.That(effectiveEta, Is.EqualTo(erpDate));
        Assert.That(source, Is.EqualTo("ERP"));
    }
}
