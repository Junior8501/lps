namespace LPS.APS.Core.Dto;

/// <summary>
/// Timed Supply标准事实DTO（5号位→2号位冻结接口）
/// 用于表达采购/VMI/到厂未入库等时间相关的供给源
/// 字段严格遵循2↔5接口冻结标准，不随意扩张
/// </summary>
public sealed class TimedSupplyFact
{
    /// <summary>
    /// 供给类型（冻结值域：PURCHASE_IN_TRANSIT / OPEN_PO_REMAINING / ARRIVED_NOT_RECEIVED / VMI_ONSITE 等）
    /// </summary>
    public string SupplyType { get; init; } = string.Empty;

    /// <summary>
    /// 物理来源键（PO号/VMI仓库号/Transit单号等，用于去重与追溯）
    /// </summary>
    public string PhysicalSourceKey { get; init; } = string.Empty;

    /// <summary>
    /// 物料ID
    /// </summary>
    public int MaterialId { get; init; }

    /// <summary>
    /// 物料编码
    /// </summary>
    public string MaterialCode { get; init; } = string.Empty;

    /// <summary>
    /// 接收工厂ID
    /// </summary>
    public int FactoryId { get; init; }

    /// <summary>
    /// 工厂编码
    /// </summary>
    public string FactoryCode { get; init; } = string.Empty;

    /// <summary>
    /// 仓库编码
    /// </summary>
    public string WarehouseCode { get; init; } = string.Empty;

    /// <summary>
    /// 剩余可用数量
    /// </summary>
    public decimal RemainingQty { get; init; }

    /// <summary>
    /// ETA（预计到达时间，已完成优先级计算的最终生效ETA）
    /// </summary>
    public DateTime? Eta { get; init; }

    /// <summary>
    /// 排程可用时间（ETA + ArrivalToUsableOffset）
    /// 2号位按此时间消费Supply
    /// </summary>
    public DateTime? AvailableTime { get; init; }

    /// <summary>
    /// 承诺状态（V1表示正式供应事实承诺状态）
    /// </summary>
    public string CommitmentStatus { get; init; } = string.Empty;

    /// <summary>
    /// 可信度
    /// </summary>
    public string Confidence { get; init; } = string.Empty;

    /// <summary>
    /// 来源单据号
    /// </summary>
    public string SourceDocumentNo { get; init; } = string.Empty;

    /// <summary>
    /// 来源单据行号
    /// </summary>
    public string SourceDocumentLineNo { get; init; } = string.Empty;

    /// <summary>
    /// 来源数据更新时间（用于数据新鲜度判断）
    /// </summary>
    public DateTime SourceUpdatedAt { get; init; }
}
