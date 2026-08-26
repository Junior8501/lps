using LPS.APS.BusinessRules.Repositories;
using LPS.APS.Core.Dto;

namespace LPS.APS.BusinessRules.Services;

/// <summary>
/// 采购人工ETA业务服务
///
/// 【职责边界 - 2026-08-26】
/// - 5号位提供此服务供4号位前端调用
/// - 封装Repository，提供业务层验证和操作
/// - 不负责ETA计算（属于2号位职责）
///
/// 参考：复审报告P1-01
/// </summary>
public class ProcurementManualEtaService
{
    private readonly IProcurementManualEtaRepository _repository;

    public ProcurementManualEtaService(IProcurementManualEtaRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <summary>
    /// 查询Manual ETA记录
    /// </summary>
    public async Task<List<ProcurementManualEtaOverride>> QueryAsync(
        List<int>? materialIds = null,
        List<string>? poNos = null,
        bool activeOnly = true,
        CancellationToken ct = default)
    {
        return await _repository.QueryAsync(materialIds, poNos, activeOnly, ct);
    }

    /// <summary>
    /// 根据业务键查询单条记录
    /// </summary>
    public async Task<ProcurementManualEtaOverride?> GetByBusinessKeyAsync(
        string poNo,
        int lineNo,
        int materialId,
        string receivingWarehouse,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(poNo))
            throw new ArgumentException("PONo cannot be empty", nameof(poNo));
        if (lineNo <= 0)
            throw new ArgumentException("LineNo must be positive", nameof(lineNo));
        if (materialId <= 0)
            throw new ArgumentException("MaterialId must be positive", nameof(materialId));
        if (string.IsNullOrWhiteSpace(receivingWarehouse))
            throw new ArgumentException("ReceivingWarehouse cannot be empty", nameof(receivingWarehouse));

        return await _repository.GetByBusinessKeyAsync(poNo, lineNo, materialId, receivingWarehouse, ct);
    }

    /// <summary>
    /// 新增或更新Manual ETA
    /// </summary>
    public async Task UpsertAsync(ProcurementManualEtaOverride etaOverride, CancellationToken ct = default)
    {
        if (etaOverride == null)
            throw new ArgumentNullException(nameof(etaOverride));

        // 业务验证
        if (string.IsNullOrWhiteSpace(etaOverride.PONo))
            throw new ArgumentException("PONo cannot be empty");
        if (etaOverride.LineNo <= 0)
            throw new ArgumentException("LineNo must be positive");
        if (etaOverride.MaterialId <= 0)
            throw new ArgumentException("MaterialId must be positive");
        if (string.IsNullOrWhiteSpace(etaOverride.ReceivingWarehouse))
            throw new ArgumentException("ReceivingWarehouse cannot be empty");
        if (string.IsNullOrWhiteSpace(etaOverride.UpdatedBy))
            throw new ArgumentException("UpdatedBy cannot be empty");

        // ManualEta不能早于当前时间太多（业务规则：允许最多回溯7天）
        var minAllowedEta = DateTime.UtcNow.AddDays(-7);
        if (etaOverride.ManualEta < minAllowedEta)
            throw new ArgumentException($"ManualEta cannot be earlier than {minAllowedEta:yyyy-MM-dd}");

        await _repository.UpsertAsync(etaOverride, ct);
    }

    /// <summary>
    /// 取消Manual ETA（设置IsActive=0）
    /// </summary>
    public async Task<bool> CancelAsync(
        string poNo,
        int lineNo,
        int materialId,
        string receivingWarehouse,
        string updatedBy,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(poNo))
            throw new ArgumentException("PONo cannot be empty", nameof(poNo));
        if (lineNo <= 0)
            throw new ArgumentException("LineNo must be positive", nameof(lineNo));
        if (materialId <= 0)
            throw new ArgumentException("MaterialId must be positive", nameof(materialId));
        if (string.IsNullOrWhiteSpace(receivingWarehouse))
            throw new ArgumentException("ReceivingWarehouse cannot be empty", nameof(receivingWarehouse));
        if (string.IsNullOrWhiteSpace(updatedBy))
            throw new ArgumentException("UpdatedBy cannot be empty", nameof(updatedBy));

        return await _repository.CancelAsync(poNo, lineNo, materialId, receivingWarehouse, updatedBy, ct);
    }

    /// <summary>
    /// 批量取消指定PO的所有Manual ETA
    /// </summary>
    public async Task<int> CancelByPoNoAsync(
        string poNo,
        string updatedBy,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(poNo))
            throw new ArgumentException("PONo cannot be empty", nameof(poNo));
        if (string.IsNullOrWhiteSpace(updatedBy))
            throw new ArgumentException("UpdatedBy cannot be empty", nameof(updatedBy));

        // 查询该PO下所有活动的Manual ETA
        var records = await _repository.QueryAsync(null, new List<string> { poNo }, activeOnly: true, ct);

        int canceledCount = 0;
        foreach (var record in records)
        {
            var success = await _repository.CancelAsync(
                record.PONo,
                record.LineNo,
                record.MaterialId,
                record.ReceivingWarehouse,
                updatedBy,
                ct);

            if (success)
                canceledCount++;
        }

        return canceledCount;
    }
}
