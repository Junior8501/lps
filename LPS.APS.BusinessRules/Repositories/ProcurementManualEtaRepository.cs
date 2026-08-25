using LPS.APS.Core.Dto;
using LPS.APS.Engine.Data;
using System.Data;

namespace LPS.APS.BusinessRules.Repositories;

/// <summary>
/// 采购人工ETA Repository实现
///
/// 【实现说明】
/// - 表名：ProcurementManualEtaOverride
/// - 业务键：PONo + LineNo + MaterialId + ReceivingWarehouse
/// - 取消方式：IsActive=0（不物理删除）
///
/// 参考：APS_V1_5号位新基线增量整改开发包_v1.0_20260825.md P0-2
/// </summary>
public class ProcurementManualEtaRepository : IProcurementManualEtaRepository
{
    private readonly DatabaseConnectionManager _connectionManager;

    public ProcurementManualEtaRepository(DatabaseConnectionManager connectionManager)
    {
        _connectionManager = connectionManager ?? throw new ArgumentNullException(nameof(connectionManager));
    }

    public async Task<List<ProcurementManualEtaOverride>> QueryAsync(
        List<int>? materialIds = null,
        List<string>? poNos = null,
        bool activeOnly = true,
        CancellationToken ct = default)
    {
        var sql = @"
SELECT
    PONo, LineNo, MaterialId, MaterialCode, ReceivingWarehouse,
    ManualEta, IsActive, UpdatedBy, UpdatedAt
FROM ProcurementManualEtaOverride
WHERE 1=1
    AND (@ActiveOnly = 0 OR IsActive = 1)
    AND (@MaterialIds IS NULL OR MaterialId IN @MaterialIds)
    AND (@PONos IS NULL OR PONo IN @PONos)
ORDER BY UpdatedAt DESC";

        var parameters = new
        {
            ActiveOnly = activeOnly ? 1 : 0,
            MaterialIds = materialIds,
            PONos = poNos
        };

        var results = await _connectionManager.QueryAsync<ProcurementManualEtaOverride>(
            sql,
            parameters,
            CommandType.Text,
            DatabaseId.ODS,
            commandTimeout: 30);

        return results.ToList();
    }

    public async Task<ProcurementManualEtaOverride?> GetByBusinessKeyAsync(
        string poNo,
        int lineNo,
        int materialId,
        string receivingWarehouse,
        CancellationToken ct = default)
    {
        var sql = @"
SELECT
    PONo, LineNo, MaterialId, MaterialCode, ReceivingWarehouse,
    ManualEta, IsActive, UpdatedBy, UpdatedAt
FROM ProcurementManualEtaOverride
WHERE PONo = @PONo
    AND LineNo = @LineNo
    AND MaterialId = @MaterialId
    AND ReceivingWarehouse = @ReceivingWarehouse";

        var parameters = new
        {
            PONo = poNo,
            LineNo = lineNo,
            MaterialId = materialId,
            ReceivingWarehouse = receivingWarehouse
        };

        var results = await _connectionManager.QueryAsync<ProcurementManualEtaOverride>(
            sql,
            parameters,
            CommandType.Text,
            DatabaseId.ODS,
            commandTimeout: 10);

        return results.FirstOrDefault();
    }

    public async Task UpsertAsync(ProcurementManualEtaOverride @override, CancellationToken ct = default)
    {
        var sql = @"
MERGE INTO ProcurementManualEtaOverride AS target
USING (SELECT
    @PONo AS PONo,
    @LineNo AS LineNo,
    @MaterialId AS MaterialId,
    @ReceivingWarehouse AS ReceivingWarehouse
) AS source
ON target.PONo = source.PONo
    AND target.LineNo = source.LineNo
    AND target.MaterialId = source.MaterialId
    AND target.ReceivingWarehouse = source.ReceivingWarehouse
WHEN MATCHED THEN
    UPDATE SET
        ManualEta = @ManualEta,
        IsActive = @IsActive,
        UpdatedBy = @UpdatedBy,
        UpdatedAt = GETDATE(),
        Remark = @Remark
WHEN NOT MATCHED THEN
    INSERT (PONo, LineNo, MaterialId, MaterialCode, ReceivingWarehouse,
            ManualEta, IsActive, UpdatedBy, UpdatedAt, CreatedBy, CreatedAt, Remark)
    VALUES (@PONo, @LineNo, @MaterialId, @MaterialCode, @ReceivingWarehouse,
            @ManualEta, @IsActive, @UpdatedBy, GETDATE(), @UpdatedBy, GETDATE(), @Remark);";

        var parameters = new
        {
            PONo = @override.PONo,
            LineNo = @override.LineNo,
            MaterialId = @override.MaterialId,
            MaterialCode = @override.MaterialCode,
            ReceivingWarehouse = @override.ReceivingWarehouse,
            ManualEta = @override.ManualEta,
            IsActive = @override.IsActive ? 1 : 0,
            UpdatedBy = @override.UpdatedBy,
            Remark = (object?)null  // 暂不支持Remark，ProcurementManualEtaOverride DTO中没有该字段
        };

        await _connectionManager.ExecuteAsync(
            sql,
            parameters,
            CommandType.Text,
            DatabaseId.ODS,
            commandTimeout: 30);
    }

    public async Task<bool> CancelAsync(
        string poNo,
        int lineNo,
        int materialId,
        string receivingWarehouse,
        string updatedBy,
        CancellationToken ct = default)
    {
        var sql = @"
UPDATE ProcurementManualEtaOverride
SET IsActive = 0,
    UpdatedBy = @UpdatedBy,
    UpdatedAt = GETDATE()
WHERE PONo = @PONo
    AND LineNo = @LineNo
    AND MaterialId = @MaterialId
    AND ReceivingWarehouse = @ReceivingWarehouse;";

        var parameters = new
        {
            PONo = poNo,
            LineNo = lineNo,
            MaterialId = materialId,
            ReceivingWarehouse = receivingWarehouse,
            UpdatedBy = updatedBy
        };

        var rowCount = await _connectionManager.ExecuteAsync(
            sql,
            parameters,
            CommandType.Text,
            DatabaseId.ODS,
            commandTimeout: 10);

        return rowCount > 0;
    }
}
