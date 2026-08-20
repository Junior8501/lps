using LPS.APS.BusinessRules.Calculators;
using LPS.APS.BusinessRules.Converters;
using LPS.APS.BusinessRules.Loaders;
using LPS.APS.Core.Dto;

namespace LPS.APS.BusinessRules.Services;

public class Position5SupplyService
{
    private readonly TimedSupplyFactLoader _loader;
    private readonly ProcurementEtaCalculator _etaCalculator;
    private readonly ProcurementFactConverter _converter;

    public Position5SupplyService(
        TimedSupplyFactLoader loader,
        ProcurementEtaCalculator etaCalculator,
        ProcurementFactConverter converter)
    {
        _loader = loader ?? throw new ArgumentNullException(nameof(loader));
        _etaCalculator = etaCalculator ?? throw new ArgumentNullException(nameof(etaCalculator));
        _converter = converter ?? throw new ArgumentNullException(nameof(converter));
    }

    public async Task<Position5SupplyResult> LoadProcurementSupplyAsync(
        SupplyFactScope scope,
        CancellationToken ct)
    {
        if (scope == null)
            throw new ArgumentNullException(nameof(scope));

        var result = new Position5SupplyResult
        {
            Scope = scope,
            LoadStartTime = DateTime.UtcNow
        };

        try
        {
            var rawFacts = await _loader.LoadRawFactsAsync(scope, ct);
            result.RawFactCount = rawFacts.Count;

            var validFacts = new List<TimedSupplyFact>();
            var issues = new List<Position5Issue>();

            foreach (var rawFact in rawFacts)
            {
                try
                {
                    var timedFact = _converter.ConvertToTimedSupplyFact(rawFact);
                    validFacts.Add(timedFact);
                }
                catch (InvalidOperationException ex)
                {
                    issues.Add(new Position5Issue
                    {
                        Severity = "WARNING",
                        IssueCode = "F21",
                        PhysicalSourceKey = rawFact.PhysicalSourceKey,
                        MaterialCode = rawFact.MaterialCode,
                        FactoryCode = rawFact.FactoryCode,
                        Message = ex.Message,
                        RawSupplyType = rawFact.SupplyType,
                        DetectedAt = DateTime.UtcNow
                    });
                }
                catch (ArgumentException ex)
                {
                    issues.Add(new Position5Issue
                    {
                        Severity = "WARNING",
                        IssueCode = "F21",
                        PhysicalSourceKey = rawFact.PhysicalSourceKey,
                        MaterialCode = rawFact.MaterialCode,
                        FactoryCode = rawFact.FactoryCode,
                        Message = ex.Message,
                        RawSupplyType = rawFact.SupplyType,
                        DetectedAt = DateTime.UtcNow
                    });
                }
            }

            result.ValidFactCount = validFacts.Count;
            result.TimedSupplyFacts = validFacts;
            result.Issues = issues;
            result.LoadEndTime = DateTime.UtcNow;
            result.Success = true;

            return result;
        }
        catch (Exception ex)
        {
            result.LoadEndTime = DateTime.UtcNow;
            result.Success = false;
            result.ErrorMessage = ex.Message;
            throw;
        }
    }
}

public class Position5SupplyResult
{
    public SupplyFactScope Scope { get; set; }
    public DateTime LoadStartTime { get; set; }
    public DateTime LoadEndTime { get; set; }
    public bool Success { get; set; }
    public string ErrorMessage { get; set; }
    public int RawFactCount { get; set; }
    public int ValidFactCount { get; set; }
    public IReadOnlyList<TimedSupplyFact> TimedSupplyFacts { get; set; }
    public IReadOnlyList<Position5Issue> Issues { get; set; }

    public TimeSpan Duration => LoadEndTime - LoadStartTime;
    public int InvalidFactCount => RawFactCount - ValidFactCount;
}

public class Position5Issue
{
    public string Severity { get; set; }
    public string IssueCode { get; set; }
    public string PhysicalSourceKey { get; set; }
    public string MaterialCode { get; set; }
    public string FactoryCode { get; set; }
    public string Message { get; set; }
    public string RawSupplyType { get; set; }
    public DateTime DetectedAt { get; set; }
}
