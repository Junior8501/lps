using LPS.APS.Core.Enum;

namespace LPS.APS.Core.Dto;

/// <summary>
/// Position计算问题记录
/// </summary>
public sealed class PositionIssue
{
    /// <summary>
    /// 问题类型代码（如：STAGE_MISMATCH, QUANTITY_GAP, XC_OVERLAP等）
    /// </summary>
    public string IssueType { get; init; } = string.Empty;

    /// <summary>
    /// 问题等级
    /// </summary>
    public PositionIssueLevel Level { get; init; }

    /// <summary>
    /// 问题描述
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// 涉及的PI号
    /// </summary>
    public string ProductionInstructionNo { get; init; } = string.Empty;

    /// <summary>
    /// 涉及的Stage代码（如果适用）
    /// </summary>
    public string? StageCode { get; init; }

    /// <summary>
    /// 问题数量（如果适用）
    /// </summary>
    public decimal? AffectedQuantity { get; init; }

    /// <summary>
    /// 详细上下文信息（JSON或结构化文本）
    /// </summary>
    public string? ContextData { get; init; }
}
