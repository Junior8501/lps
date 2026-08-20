namespace LPS.APS.Core.Dto;

/// <summary>
/// 生产指示位置计算结果（5号位返回给2号位）
///
/// 每个PI的Position必须满足：Σ PositionQty = ErpRemainingQty（总量闭合）
/// </summary>
public sealed class ProductionInstructionPositionResult
{
    /// <summary>
    /// 生产指示号
    /// </summary>
    public string ProductionInstructionNo { get; init; } = string.Empty;

    /// <summary>
    /// 总剩余数量（应该等于输入的ErpRemainingQty）
    /// </summary>
    public decimal TotalRemainingQty { get; init; }

    /// <summary>
    /// Position切片列表（各个位置的数量分布）
    /// 必须保证：Σ Positions[i].Quantity = TotalRemainingQty
    /// </summary>
    public IReadOnlyList<PositionSlice> Positions { get; init; } = Array.Empty<PositionSlice>();

    /// <summary>
    /// 计算过程中发现的问题
    /// </summary>
    public IReadOnlyList<PositionIssue> Issues { get; init; } = Array.Empty<PositionIssue>();

    /// <summary>
    /// 是否计算成功（总量是否闭合）
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// 失败原因（如果IsSuccess=false）
    /// </summary>
    public string? FailureReason { get; init; }
}
