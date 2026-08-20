namespace LPS.APS.Core.Dto;

/// <summary>
/// PI级库存事实
/// </summary>
public sealed class PiInventoryFact
{
    /// <summary>
    /// 仓库代码
    /// </summary>
    public string WarehouseCode { get; init; } = string.Empty;

    /// <summary>
    /// 数量
    /// </summary>
    public decimal Quantity { get; init; }

    /// <summary>
    /// 可用时间
    /// </summary>
    public DateTime? AvailableTime { get; init; }

    /// <summary>
    /// 来源单据（如果有）
    /// </summary>
    public string? SourceDocument { get; init; }
}
