using LPS.APS.Core.Enum;

namespace LPS.APS.Core.Dto;

/// <summary>
/// Position位置切片
/// 表示PI RemainingQty在某个具体位置的数量分布
/// </summary>
public sealed class PositionSlice
{
    /// <summary>
    /// 位置类型
    /// </summary>
    public PositionType PositionType { get; init; }

    /// <summary>
    /// Stage代码（当PositionType=STAGE时）
    /// </summary>
    public string? StageCode { get; init; }

    /// <summary>
    /// 位置键（用于标识具体位置，如XC仓库代码、Transit单号等）
    /// </summary>
    public string? LocationKey { get; init; }

    /// <summary>
    /// 该位置的数量
    /// </summary>
    public decimal Quantity { get; init; }

    /// <summary>
    /// 可用时间（适用于WAITING、INTERPLANT_IN_TRANSIT等）
    /// </summary>
    public DateTime? AvailableTime { get; init; }

    /// <summary>
    /// 是否为强事实（如Received、XC等有单据支撑的事实）
    /// </summary>
    public bool IsStrongEvidence { get; init; }

    /// <summary>
    /// 来源键（用于追溯，如单据号、进度快照ID等）
    /// </summary>
    public string? SourceKey { get; init; }

    /// <summary>
    /// 是否为UNLOCATED（无法定位但数量必须闭合）
    /// </summary>
    public bool IsUnlocated { get; init; }
}
