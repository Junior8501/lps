using LPS.APS.BusinessRules.Services;
using LPS.APS.Core.Dto;
using LPS.APS.Shared.Models;
using Microsoft.AspNetCore.Mvc;

namespace LPS.APS.Web.Controllers;

/// <summary>
/// 采购人工ETA维护控制器（5号位提供，供4号位前端调用）
///
/// 路由规范：
///   GET    /api/procurement-manual-eta                  - 查询Manual ETA列表
///   GET    /api/procurement-manual-eta/{poNo}/{lineNo}  - 查询单条记录
///   POST   /api/procurement-manual-eta                  - 新增或更新Manual ETA
///   DELETE /api/procurement-manual-eta                  - 取消Manual ETA
///
/// 【职责边界 - 2026-08-26】
/// - 5号位提供Manual ETA维护API
/// - 2号位消费Manual ETA并计算Effective ETA
///
/// 参考：复审报告P1-01
/// </summary>
[ApiController]
[Route("api/procurement-manual-eta")]
public class ProcurementManualEtaController : ControllerBase
{
    private readonly ProcurementManualEtaService _service;
    private readonly ILogger<ProcurementManualEtaController> _logger;

    public ProcurementManualEtaController(
        ProcurementManualEtaService service,
        ILogger<ProcurementManualEtaController> logger)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 查询Manual ETA列表
    /// </summary>
    /// <param name="materialIds">物料ID列表（可选，逗号分隔）</param>
    /// <param name="poNos">采购订单号列表（可选，逗号分隔）</param>
    /// <param name="activeOnly">仅返回活动记录</param>
    /// <param name="cancellationToken">取消令牌</param>
    [HttpGet]
    public async Task<ApiResponse<List<ProcurementManualEtaOverride>>> Query(
        [FromQuery] string? materialIds = null,
        [FromQuery] string? poNos = null,
        [FromQuery] bool activeOnly = true,
        CancellationToken cancellationToken = default)
    {
        try
        {
            List<int>? materialIdList = null;
            if (!string.IsNullOrWhiteSpace(materialIds))
            {
                materialIdList = materialIds.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => int.Parse(x.Trim()))
                    .ToList();
            }

            List<string>? poNoList = null;
            if (!string.IsNullOrWhiteSpace(poNos))
            {
                poNoList = poNos.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.Trim())
                    .ToList();
            }

            var result = await _service.QueryAsync(materialIdList, poNoList, activeOnly, cancellationToken);
            return ApiResponse<List<ProcurementManualEtaOverride>>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to query Manual ETA records");
            return ApiResponse<List<ProcurementManualEtaOverride>>.Fail(500, $"Query failed: {ex.Message}");
        }
    }

    /// <summary>
    /// 根据业务键查询单条Manual ETA记录
    /// </summary>
    /// <param name="poNo">采购订单号</param>
    /// <param name="lineNo">行号</param>
    /// <param name="materialId">物料ID</param>
    /// <param name="receivingWarehouse">接收仓库</param>
    /// <param name="cancellationToken">取消令牌</param>
    [HttpGet("{poNo}/{lineNo}")]
    public async Task<ApiResponse<ProcurementManualEtaOverride?>> GetByBusinessKey(
        [FromRoute] string poNo,
        [FromRoute] int lineNo,
        [FromQuery] int materialId,
        [FromQuery] string receivingWarehouse,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _service.GetByBusinessKeyAsync(poNo, lineNo, materialId, receivingWarehouse, cancellationToken);
            if (result == null)
                return ApiResponse<ProcurementManualEtaOverride?>.Fail(404, "Record not found");

            return ApiResponse<ProcurementManualEtaOverride?>.Success(result);
        }
        catch (ArgumentException ex)
        {
            return ApiResponse<ProcurementManualEtaOverride?>.Fail(400, $"Invalid parameters: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get Manual ETA record");
            return ApiResponse<ProcurementManualEtaOverride?>.Fail(500, $"Query failed: {ex.Message}");
        }
    }

    /// <summary>
    /// 新增或更新Manual ETA
    /// </summary>
    [HttpPost]
    public async Task<ApiResponse<string>> Upsert(
        [FromBody] ProcurementManualEtaOverride etaOverride,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _service.UpsertAsync(etaOverride, cancellationToken);
            return ApiResponse<string>.Success("Manual ETA saved successfully");
        }
        catch (ArgumentException ex)
        {
            return ApiResponse<string>.Fail(400, $"Validation failed: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upsert Manual ETA");
            return ApiResponse<string>.Fail(500, $"Save failed: {ex.Message}");
        }
    }

    /// <summary>
    /// 取消Manual ETA（设置IsActive=0）
    /// </summary>
    [HttpDelete]
    public async Task<ApiResponse<string>> Cancel(
        [FromQuery] string poNo,
        [FromQuery] int lineNo,
        [FromQuery] int materialId,
        [FromQuery] string receivingWarehouse,
        [FromQuery] string updatedBy,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var success = await _service.CancelAsync(poNo, lineNo, materialId, receivingWarehouse, updatedBy, cancellationToken);
            if (!success)
                return ApiResponse<string>.Fail(404, "Record not found or already inactive");

            return ApiResponse<string>.Success("Manual ETA canceled successfully");
        }
        catch (ArgumentException ex)
        {
            return ApiResponse<string>.Fail(400, $"Invalid parameters: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cancel Manual ETA");
            return ApiResponse<string>.Fail(500, $"Cancel failed: {ex.Message}");
        }
    }
}
