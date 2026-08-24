using LPS.APS.BusinessRules.Calculators;
using LPS.APS.BusinessRules.Loaders;
using LPS.APS.BusinessRules.Models;
using LPS.APS.Core.Dto;

namespace LPS.APS.BusinessRules.Services;

public class Position5SupplyService
{
    private readonly TimedSupplyFactLoader _loader;
    private readonly TimedSupplyFactCalculator _calculator;

    public Position5SupplyService(
        TimedSupplyFactLoader loader,
        TimedSupplyFactCalculator calculator)
    {
        _loader = loader ?? throw new ArgumentNullException(nameof(loader));
        _calculator = calculator ?? throw new ArgumentNullException(nameof(calculator));
    }

    public async Task<Position5SupplyResult> LoadProcurementSupplyAsync(
        SupplyFactScope scope,
        FrozenFactParameters parameters,
        CancellationToken ct)
    {
        if (scope == null)
            throw new ArgumentNullException(nameof(scope));
        if (parameters == null)
            throw new ArgumentNullException(nameof(parameters));

        var result = new Position5SupplyResult
        {
            Scope = scope,
            LoadStartTime = DateTime.UtcNow
        };

        try
        {
            var rawFacts = await _loader.LoadRawFactsAsync(scope, ct);
            result.RawFactCount = rawFacts.Count;

            // Service Overlay: 装载ManualEta覆盖（Loader后Service叠加）
            var manualEtaOverrides = await LoadManualEtaOverridesAsync(scope, ct);

            var validFacts = new List<TimedSupplyFact>();
            var issues = new List<Position5Issue>();
            var referenceTime = DateTime.UtcNow;

            foreach (var rawFact in rawFacts)
            {
                try
                {
                    var timedFact = _calculator.CalculateEffectiveSupply(rawFact, parameters, referenceTime);

                    // Service Overlay: 如果存在ManualEta覆盖，替换Eta字段
                    var overlayedFact = ApplyManualEtaOverlay(timedFact, manualEtaOverrides);
                    validFacts.Add(overlayedFact);
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

    /// <summary>
    /// 装载ManualEta覆盖事实（Service Overlay）
    /// </summary>
    private async Task<Dictionary<string, ProcurementManualEtaOverride>> LoadManualEtaOverridesAsync(
        SupplyFactScope scope,
        CancellationToken ct)
    {
        // TODO: 实现从ProcurementManualEtaOverride表装载
        // Key格式: "{PONo}|{LineNo}|{MaterialId}|{ReceivingWarehouse}"
        // 当前返回空字典作为桩实现
        await Task.CompletedTask;
        return new Dictionary<string, ProcurementManualEtaOverride>();
    }

    /// <summary>
    /// 应用ManualEta覆盖（Service Overlay）
    /// 优先级公式（冻结）：ManualEta ?? ErpEta ?? ReleaseDate + DefaultLT
    /// </summary>
    private TimedSupplyFact ApplyManualEtaOverlay(
        TimedSupplyFact originalFact,
        Dictionary<string, ProcurementManualEtaOverride> overrides)
    {
        // 构造查找键：PONo|LineNo|MaterialId|Warehouse
        // PhysicalSourceKey通常是PONo或PONo-LineNo格式
        var key = $"{originalFact.SourceDocumentNo}|{originalFact.SourceDocumentLineNo}|{originalFact.MaterialId}|{originalFact.WarehouseCode}";

        if (overrides.TryGetValue(key, out var manualOverride) && manualOverride.IsActive)
        {
            // 创建新的TimedSupplyFact，替换Eta和AvailableTime
            // AvailableTime需要重新计算（ManualEta + ArrivalToUsableOffset）
            // 简化处理：假设offset已在原Eta→AvailableTime中体现，直接加相同offset
            var offset = originalFact.AvailableTime.HasValue && originalFact.Eta.HasValue
                ? originalFact.AvailableTime.Value - originalFact.Eta.Value
                : TimeSpan.Zero;

            return new TimedSupplyFact
            {
                SupplyType = originalFact.SupplyType,
                PhysicalSourceKey = originalFact.PhysicalSourceKey,
                MaterialId = originalFact.MaterialId,
                MaterialCode = originalFact.MaterialCode,
                FactoryId = originalFact.FactoryId,
                FactoryCode = originalFact.FactoryCode,
                WarehouseCode = originalFact.WarehouseCode,
                RemainingQty = originalFact.RemainingQty,
                Eta = manualOverride.ManualEta,  // 使用ManualEta覆盖
                AvailableTime = manualOverride.ManualEta + offset,  // 重新计算AvailableTime
                Commitment = originalFact.Commitment,
                Confidence = originalFact.Confidence,
                SourceDocumentNo = originalFact.SourceDocumentNo,
                SourceDocumentLineNo = originalFact.SourceDocumentLineNo,
                SourceUpdatedAt = originalFact.SourceUpdatedAt
            };
        }

        // 无覆盖，返回原Fact
        return originalFact;
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
