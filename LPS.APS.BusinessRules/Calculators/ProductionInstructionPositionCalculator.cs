using LPS.APS.Core.Dto;
using LPS.APS.Core.Enum;
using LPS.APS.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace LPS.APS.BusinessRules.Calculators;

/// <summary>
/// 生产指示位置计算器（5号位核心能力）
///
/// 职责边界：
///   - 接收2号位装载好的完整事实包（ProductionInstructionPositionInput）
///   - 进行纯计算：Stage差分、XC/Transit互斥、UNLOCATED、总量闭合、Issue生成
///   - 返回Position结果（ProductionInstructionPositionResult）
///   - 不访问数据库，不注入Repository
///   - 不决定PI最终分配给哪个Demand（由2号位负责）
///
/// 设计原则：
///   - DTO进、Result出，纯计算逻辑
///   - 2号位负责数据装载和DataCutoffTime一致性
///   - 5号位只负责复杂位置判断
/// </summary>
public class ProductionInstructionPositionCalculator : IProductionInstructionPositionCalculator
{
    private readonly ILogger<ProductionInstructionPositionCalculator> _logger;

    public ProductionInstructionPositionCalculator(ILogger<ProductionInstructionPositionCalculator> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ProductionInstructionPositionResult>> CalculatePositionsAsync(
        IReadOnlyList<ProductionInstructionPositionInput> inputs,
        CancellationToken cancellationToken = default)
    {
        var results = new List<ProductionInstructionPositionResult>();

        foreach (var input in inputs)
        {
            try
            {
                var result = CalculateSinglePiPosition(input);
                results.Add(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "PI Position计算失败: PI={PiNo}, Material={MatId}, Factory={FactId}",
                    input.ProductionInstructionNo, input.MaterialId, input.FactoryId);

                results.Add(new ProductionInstructionPositionResult
                {
                    ProductionInstructionNo = input.ProductionInstructionNo,
                    TotalRemainingQty = input.ErpRemainingQty,
                    IsSuccess = false,
                    FailureReason = $"计算异常: {ex.Message}",
                    Positions = Array.Empty<PositionSlice>(),
                    Issues = new[]
                    {
                        new PositionIssue
                        {
                            IssueType = "CALCULATION_EXCEPTION",
                            Level = PositionIssueLevel.ERROR,
                            Description = $"PI Position计算发生异常",
                            ProductionInstructionNo = input.ProductionInstructionNo,
                            ContextData = ex.ToString()
                        }
                    }
                });
            }
        }

        return Task.FromResult<IReadOnlyList<ProductionInstructionPositionResult>>(results);
    }

    /// <summary>
    /// 计算单个PI的Position
    /// </summary>
    private ProductionInstructionPositionResult CalculateSinglePiPosition(ProductionInstructionPositionInput input)
    {
        var issues = new List<PositionIssue>();
        var positions = new List<PositionSlice>();

        // 第一步：计算Stage位置（累计差分）
        var stagePositions = CalculateStagePositions(input, issues);
        positions.AddRange(stagePositions);

        // 第二步：处理XC位置
        var xcPositions = CalculateXcPositions(input, issues);
        positions.AddRange(xcPositions);

        // 第三步：处理厂间在途
        var transitPositions = CalculateTransitPositions(input, issues);
        positions.AddRange(transitPositions);

        // 第四步：处理强事实
        ApplyStrongFacts(input, positions, issues);

        // 第五步：Position互斥消重
        var deduplicatedPositions = DeduplicatePositions(positions, issues);

        // 第六步：计算UNLOCATED并总量闭合
        var finalPositions = EnsureTotalClosure(
            input.ErpRemainingQty,
            deduplicatedPositions,
            input.ProductionInstructionNo,
            issues);

        // 第七步：校验总量是否闭合
        decimal totalQty = finalPositions.Sum(p => p.Quantity);
        bool isSuccess = Math.Abs(totalQty - input.ErpRemainingQty) < 0.0001m;

        if (!isSuccess)
        {
            issues.Add(new PositionIssue
            {
                IssueType = "QUANTITY_NOT_CLOSED",
                Level = PositionIssueLevel.ERROR,
                Description = $"Position总量无法闭合: ERP={input.ErpRemainingQty}, 计算总量={totalQty}, 差额={input.ErpRemainingQty - totalQty}",
                ProductionInstructionNo = input.ProductionInstructionNo,
                AffectedQuantity = input.ErpRemainingQty - totalQty
            });
        }

        return new ProductionInstructionPositionResult
        {
            ProductionInstructionNo = input.ProductionInstructionNo,
            TotalRemainingQty = input.ErpRemainingQty,
            Positions = finalPositions,
            Issues = issues,
            IsSuccess = isSuccess,
            FailureReason = isSuccess ? null : "Position总量无法与ERP RemainingQty闭合"
        };
    }

    /// <summary>
    /// 计算Stage位置（累计差分）
    /// </summary>
    private List<PositionSlice> CalculateStagePositions(
        ProductionInstructionPositionInput input,
        List<PositionIssue> issues)
    {
        var stagePositions = new List<PositionSlice>();

        if (input.StageProgress == null || input.StageProgress.Count == 0)
        {
            return stagePositions;
        }

        // 按Stage序号排序
        var sortedStages = input.StageProgress
            .OrderBy(s => s.StageSequence)
            .ToList();

        // 检查下游累计大于上游的情况
        for (int i = 0; i < sortedStages.Count - 1; i++)
        {
            var currentStage = sortedStages[i];
            var nextStage = sortedStages[i + 1];

            if (nextStage.CumulativeCompletedQty > currentStage.CumulativeCompletedQty)
            {
                issues.Add(new PositionIssue
                {
                    IssueType = "DOWNSTREAM_GT_UPSTREAM",
                    Level = PositionIssueLevel.WARN,
                    Description = $"下游Stage累计量({nextStage.CumulativeCompletedQty})大于上游Stage累计量({currentStage.CumulativeCompletedQty})",
                    ProductionInstructionNo = input.ProductionInstructionNo,
                    StageCode = nextStage.StageCode,
                    AffectedQuantity = nextStage.CumulativeCompletedQty - currentStage.CumulativeCompletedQty,
                    ContextData = $"上游Stage: {currentStage.StageCode}, 下游Stage: {nextStage.StageCode}"
                });

                // 保守处理：下修下游有效累计量
                var correctedStage = new StageProgressFact
                {
                    StageCode = nextStage.StageCode,
                    CumulativeCompletedQty = currentStage.CumulativeCompletedQty,
                    StageSequence = nextStage.StageSequence,
                    SnapshotId = nextStage.SnapshotId,
                    UpdatedAt = nextStage.UpdatedAt
                };
                sortedStages[i + 1] = correctedStage;
            }
        }

        // 计算每个Stage的区间数量（差分）
        for (int i = sortedStages.Count - 1; i >= 0; i--)
        {
            decimal qty;
            if (i == sortedStages.Count - 1)
            {
                // 最后一个Stage：累计量就是该Stage的数量
                qty = sortedStages[i].CumulativeCompletedQty;
            }
            else
            {
                // 中间Stage：本Stage累计量 - 下游Stage累计量
                qty = sortedStages[i].CumulativeCompletedQty - sortedStages[i + 1].CumulativeCompletedQty;
            }

            if (qty > 0.0001m)  // 只记录有数量的Stage
            {
                stagePositions.Add(new PositionSlice
                {
                    PositionType = PositionType.STAGE,
                    StageCode = sortedStages[i].StageCode,
                    Quantity = qty,
                    IsStrongEvidence = false,
                    SourceKey = sortedStages[i].SnapshotId?.ToString(),
                    IsUnlocated = false
                });
            }
        }

        return stagePositions;
    }

    /// <summary>
    /// 计算XC位置
    /// </summary>
    private List<PositionSlice> CalculateXcPositions(
        ProductionInstructionPositionInput input,
        List<PositionIssue> issues)
    {
        var xcPositions = new List<PositionSlice>();

        if (input.XcFacts == null || input.XcFacts.Count == 0)
        {
            return xcPositions;
        }

        foreach (var xc in input.XcFacts)
        {
            if (xc.Quantity > 0.0001m)
            {
                xcPositions.Add(new PositionSlice
                {
                    PositionType = PositionType.XC,
                    LocationKey = xc.XcWarehouseCode,
                    StageCode = xc.RelatedStageCode,
                    Quantity = xc.Quantity,
                    AvailableTime = xc.AvailableTime,
                    IsStrongEvidence = true,  // XC是强事实
                    SourceKey = xc.SourceDocument,
                    IsUnlocated = false
                });
            }
        }

        return xcPositions;
    }

    /// <summary>
    /// 计算厂间在途位置
    ///
    /// F10-F12: 实现SH号匹配和Transit/Received去重
    /// - 同SH号的Received会扣减对应Transit数量
    /// - 不同SH号不会串用
    /// - 已Received的物理份额不会重复计入Transit
    /// </summary>
    private List<PositionSlice> CalculateTransitPositions(
        ProductionInstructionPositionInput input,
        List<PositionIssue> issues)
    {
        var transitPositions = new List<PositionSlice>();

        if (input.TransitFacts == null || input.TransitFacts.Count == 0)
        {
            return transitPositions;
        }

        // 构建SH号 -> 剩余未消耗Received数量的映射（用于去重）
        // 只处理DocumentType="SH"的Received（跨厂物流到货）
        var remainingReceivedBySH = new Dictionary<string, decimal>();
        if (input.StrongFacts != null)
        {
            foreach (var received in input.StrongFacts)
            {
                if (!string.IsNullOrEmpty(received.DocumentNo) &&
                    received.DocumentType == "SH")
                {
                    if (!remainingReceivedBySH.ContainsKey(received.DocumentNo))
                    {
                        remainingReceivedBySH[received.DocumentNo] = 0m;
                    }
                    remainingReceivedBySH[received.DocumentNo] += received.Quantity;
                }
            }
        }

        // 处理每个Transit，扣除已Received部分
        foreach (var transit in input.TransitFacts)
        {
            if (transit.Quantity <= 0.0001m)
            {
                continue;
            }

            decimal effectiveTransitQty = transit.Quantity;

            // F10/F11/F12: 检查是否有同SH号的Received
            if (!string.IsNullOrEmpty(transit.SourceDocument) &&
                remainingReceivedBySH.TryGetValue(transit.SourceDocument, out decimal remainingReceivedQty) &&
                remainingReceivedQty > 0.0001m)
            {
                // 计算本次可以扣减的数量（取Transit和剩余Received的较小值）
                decimal deductQty = Math.Min(transit.Quantity, remainingReceivedQty);
                effectiveTransitQty = transit.Quantity - deductQty;

                // 更新剩余Received数量
                remainingReceivedBySH[transit.SourceDocument] = remainingReceivedQty - deductQty;

                if (effectiveTransitQty < 0.0001m)
                {
                    // 该Transit已完全Received，不再计入Position
                    _logger.LogDebug(
                        "Transit {TransitDoc} SH={SH} 已完全到货，不计入在途Position",
                        transit.TransitDocumentNo,
                        transit.SourceDocument);
                    continue;
                }
                else if (effectiveTransitQty < transit.Quantity)
                {
                    // 部分Received
                    _logger.LogDebug(
                        "Transit {TransitDoc} SH={SH} 部分到货，原始={Original}，已扣={Deducted}，剩余在途={Remaining}",
                        transit.TransitDocumentNo,
                        transit.SourceDocument,
                        transit.Quantity,
                        deductQty,
                        effectiveTransitQty);
                }
            }

            // 只有剩余在途数量>0才加入Position
            if (effectiveTransitQty > 0.0001m)
            {
                transitPositions.Add(new PositionSlice
                {
                    PositionType = PositionType.INTERPLANT_IN_TRANSIT,
                    LocationKey = $"{transit.SourceFactoryCode}→{transit.TargetFactoryCode}",
                    Quantity = effectiveTransitQty,
                    AvailableTime = transit.EstimatedArrivalTime,
                    IsStrongEvidence = true,
                    SourceKey = transit.TransitDocumentNo,
                    IsUnlocated = false
                });
            }
        }

        return transitPositions;
    }

    /// <summary>
    /// 应用强事实校正
    ///
    /// 强事实（如ReceivedFact）可以直接修正Position的数量
    /// 例如：MES已报工数量可以直接扣减Stage累计进度
    /// </summary>
    private void ApplyStrongFacts(
        ProductionInstructionPositionInput input,
        List<PositionSlice> positions,
        List<PositionIssue> issues)
    {
        // 处理StrongFacts：已报工数量
        if (input.StrongFacts != null && input.StrongFacts.Count > 0)
        {
            foreach (var received in input.StrongFacts)
            {
                if (received.Quantity > 0.0001m)
                {
                    // 找到对应Stage的Position
                    var stagePosition = positions
                        .FirstOrDefault(p => p.PositionType == PositionType.STAGE && p.StageCode == received.RelatedStageCode);

                    if (stagePosition != null)
                    {
                        // 从Stage Position中扣除已报工数量
                        decimal adjustedQty = stagePosition.Quantity - received.Quantity;

                        if (adjustedQty >= -0.0001m)
                        {
                            // 扣除后数量>=0，更新Position
                            int index = positions.IndexOf(stagePosition);
                            if (adjustedQty > 0.0001m)
                            {
                                positions[index] = new PositionSlice
                                {
                                    PositionType = stagePosition.PositionType,
                                    StageCode = stagePosition.StageCode,
                                    LocationKey = stagePosition.LocationKey,
                                    Quantity = adjustedQty,
                                    AvailableTime = stagePosition.AvailableTime,
                                    IsStrongEvidence = stagePosition.IsStrongEvidence,
                                    SourceKey = stagePosition.SourceKey,
                                    IsUnlocated = stagePosition.IsUnlocated
                                };
                            }
                            else
                            {
                                // 扣除后数量=0，移除Position
                                positions.RemoveAt(index);
                            }
                        }
                        else
                        {
                            // 报工数量超过Stage数量，记录Issue
                            issues.Add(new PositionIssue
                            {
                                IssueType = "RECEIVED_EXCEEDS_STAGE",
                                Level = PositionIssueLevel.WARN,
                                Description = $"Stage {received.RelatedStageCode} 已报工数量({received.Quantity})超过Stage Position数量({stagePosition.Quantity})",
                                ProductionInstructionNo = input.ProductionInstructionNo,
                                StageCode = received.RelatedStageCode,
                                AffectedQuantity = -adjustedQty
                            });

                            // 移除被完全消耗的Stage Position
                            positions.Remove(stagePosition);
                        }
                    }
                    else
                    {
                        // 没有对应的Stage Position，记录Issue
                        issues.Add(new PositionIssue
                        {
                            IssueType = "RECEIVED_WITHOUT_STAGE",
                            Level = PositionIssueLevel.INFO,
                            Description = $"Stage {received.RelatedStageCode} 有报工记录({received.Quantity})但无对应Stage Position",
                            ProductionInstructionNo = input.ProductionInstructionNo,
                            StageCode = received.RelatedStageCode,
                            AffectedQuantity = received.Quantity
                        });
                    }
                }
            }
        }
    }

    /// <summary>
    /// Position互斥消重
    /// 同一物理份额不能同时算在Stage、XC和Transit
    ///
    /// 消重规则：
    ///   1. 强事实（XC、Transit）优先级高于弱推导（Stage）
    ///   2. 同Stage的XC会从该Stage Position中扣除
    ///   3. Transit视为独立Position，不与Stage重叠
    /// </summary>
    private List<PositionSlice> DeduplicatePositions(
        List<PositionSlice> positions,
        List<PositionIssue> issues)
    {
        // 阶段A：按Stage分组，扣除XC数量
        var stagePositions = positions
            .Where(p => p.PositionType == PositionType.STAGE)
            .ToList();

        var xcPositions = positions
            .Where(p => p.PositionType == PositionType.XC)
            .ToList();

        var transitPositions = positions
            .Where(p => p.PositionType == PositionType.INTERPLANT_IN_TRANSIT)
            .ToList();

        var deduplicatedStages = new List<PositionSlice>();

        // 对每个Stage Position，扣除关联的XC数量
        foreach (var stage in stagePositions)
        {
            // 找到该Stage关联的XC
            var relatedXc = xcPositions
                .Where(xc => xc.StageCode == stage.StageCode)
                .Sum(xc => xc.Quantity);

            decimal adjustedQty = stage.Quantity - relatedXc;

            if (adjustedQty > 0.0001m)
            {
                // Stage数量大于XC，保留差额
                deduplicatedStages.Add(new PositionSlice
                {
                    PositionType = stage.PositionType,
                    StageCode = stage.StageCode,
                    LocationKey = stage.LocationKey,
                    Quantity = adjustedQty,
                    AvailableTime = stage.AvailableTime,
                    IsStrongEvidence = stage.IsStrongEvidence,
                    SourceKey = stage.SourceKey,
                    IsUnlocated = stage.IsUnlocated
                });
            }
            else if (adjustedQty < -0.0001m)
            {
                // XC数量超过Stage，记录异常
                issues.Add(new PositionIssue
                {
                    IssueType = "XC_EXCEEDS_STAGE",
                    Level = PositionIssueLevel.WARN,
                    Description = $"Stage {stage.StageCode} 的XC数量({relatedXc})超过Stage推导数量({stage.Quantity})",
                    StageCode = stage.StageCode,
                    AffectedQuantity = -adjustedQty
                });
                // Stage被XC完全覆盖，不保留Stage Position
            }
            // else: Stage恰好等于XC，Stage被完全覆盖，不保留
        }

        // 合并结果：去重后的Stage + 所有XC + 所有Transit
        var result = new List<PositionSlice>();
        result.AddRange(deduplicatedStages);
        result.AddRange(xcPositions);
        result.AddRange(transitPositions);

        return result;
    }

    /// <summary>
    /// 确保总量闭合，必要时添加UNLOCATED
    /// </summary>
    private List<PositionSlice> EnsureTotalClosure(
        decimal erpRemainingQty,
        List<PositionSlice> positions,
        string piNo,
        List<PositionIssue> issues)
    {
        decimal totalQty = positions.Sum(p => p.Quantity);
        decimal gap = erpRemainingQty - totalQty;

        if (Math.Abs(gap) < 0.0001m)
        {
            // 已经闭合，无需UNLOCATED
            return positions;
        }

        if (gap > 0.0001m)
        {
            // 缺口：添加UNLOCATED
            issues.Add(new PositionIssue
            {
                IssueType = "UNLOCATED_GAP",
                Level = PositionIssueLevel.WARN,
                Description = $"无法定位数量{gap}，进入UNLOCATED",
                ProductionInstructionNo = piNo,
                AffectedQuantity = gap
            });

            var unlocatedPosition = new PositionSlice
            {
                PositionType = PositionType.UNLOCATED,
                Quantity = gap,
                IsStrongEvidence = false,
                IsUnlocated = true,
                SourceKey = "AUTO_GENERATED"
            };

            return positions.Append(unlocatedPosition).ToList();
        }
        else
        {
            // 超量：严重问题
            issues.Add(new PositionIssue
            {
                IssueType = "QUANTITY_OVERFLOW",
                Level = PositionIssueLevel.ERROR,
                Description = $"Position总量({totalQty})超过ERP RemainingQty({erpRemainingQty})，超出{-gap}",
                ProductionInstructionNo = piNo,
                AffectedQuantity = -gap
            });

            return positions;
        }
    }
}
