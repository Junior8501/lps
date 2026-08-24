namespace LPS.APS.Core.Dto;

/// <summary>
/// 采购人工ETA覆盖事实（从ProcurementManualEtaOverride表装载）
///
/// 业务语义（0号位裁决）：
/// - ManualEta不是3号位参数，是业务事实（Fact）
/// - 由5号位Service叠加（Loader后Service overlay）
/// - 取消时IsActive=0，Calculator自然回退ERP ETA
///
/// 优先级公式（冻结）：
/// ManualEta ?? ErpEta ?? ReleaseDate + DefaultLT
/// 然后再叠加Warehouse Offset
/// </summary>
public sealed class ProcurementManualEtaOverride
{
    /// <summary>
    /// 采购订单号
    /// </summary>
    public string PONo { get; init; } = string.Empty;

    /// <summary>
    /// 行号
    /// </summary>
    public int LineNo { get; init; }

    /// <summary>
    /// 物料ID
    /// </summary>
    public int MaterialId { get; init; }

    /// <summary>
    /// 物料编码
    /// </summary>
    public string MaterialCode { get; init; } = string.Empty;

    /// <summary>
    /// 接收仓库
    /// </summary>
    public string ReceivingWarehouse { get; init; } = string.Empty;

    /// <summary>
    /// 人工设定的到货预期时间
    /// </summary>
    public DateTime ManualEta { get; init; }

    /// <summary>
    /// 是否生效（0=取消，回退ERP ETA）
    /// </summary>
    public bool IsActive { get; init; }

    /// <summary>
    /// 最后更新人
    /// </summary>
    public string UpdatedBy { get; init; } = string.Empty;

    /// <summary>
    /// 最后更新时间
    /// </summary>
    public DateTime UpdatedAt { get; init; }
}
