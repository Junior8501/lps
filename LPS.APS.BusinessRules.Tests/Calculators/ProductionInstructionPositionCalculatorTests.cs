using LPS.APS.BusinessRules.Calculators;
using LPS.APS.Core.Dto;
using LPS.APS.Core.Enum;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LPS.APS.BusinessRules.Tests.Calculators;

/// <summary>
/// PI Position Calculator 测试
/// 验证文档中定义的F01-F08场景
/// </summary>
public class ProductionInstructionPositionCalculatorTests
{
    private readonly ProductionInstructionPositionCalculator _calculator;

    public ProductionInstructionPositionCalculatorTests()
    {
        _calculator = new ProductionInstructionPositionCalculator(NullLogger<ProductionInstructionPositionCalculator>.Instance);
    }

    [Fact]
    public async Task F01_NormalStageProgress_ShouldCloseTo100()
    {
        var input = new ProductionInstructionPositionInput
        {
            ProductionInstructionNo = "PI-F01-001",
            MaterialId = 1001,
            FactoryId = 1,
            ErpRemainingQty = 100m,
            StageProgress = new[]
            {
                new StageProgressFact
                {
                    StageCode = "S10",
                    StageSequence = 1,
                    CumulativeCompletedQty = 80m,
                    SnapshotId = 1
                },
                new StageProgressFact
                {
                    StageCode = "S20",
                    StageSequence = 2,
                    CumulativeCompletedQty = 50m,
                    SnapshotId = 2
                },
                new StageProgressFact
                {
                    StageCode = "S30",
                    StageSequence = 3,
                    CumulativeCompletedQty = 20m,
                    SnapshotId = 3
                }
            }
        };

        var results = await _calculator.CalculatePositionsAsync(new[] { input });
        var result = results.First();

        Assert.True(result.IsSuccess);
        Assert.Equal(100m, result.Positions.Sum(p => p.Quantity));
        Assert.Equal(4, result.Positions.Count);

        var s10Position = result.Positions.First(p => p.StageCode == "S10");
        Assert.Equal(30m, s10Position.Quantity);

        var s20Position = result.Positions.First(p => p.StageCode == "S20");
        Assert.Equal(30m, s20Position.Quantity);

        var s30Position = result.Positions.First(p => p.StageCode == "S30");
        Assert.Equal(20m, s30Position.Quantity);

        var unlocatedPosition = result.Positions.First(p => p.PositionType == PositionType.UNLOCATED);
        Assert.Equal(20m, unlocatedPosition.Quantity);
    }

    [Fact]
    public async Task F02_DownstreamGreaterThanUpstream_ShouldCorrectConservatively()
    {
        var input = new ProductionInstructionPositionInput
        {
            ProductionInstructionNo = "PI-F02-001",
            MaterialId = 1002,
            FactoryId = 1,
            ErpRemainingQty = 100m,
            StageProgress = new[]
            {
                new StageProgressFact
                {
                    StageCode = "S10",
                    StageSequence = 1,
                    CumulativeCompletedQty = 60m,
                    SnapshotId = 1
                },
                new StageProgressFact
                {
                    StageCode = "S20",
                    StageSequence = 2,
                    CumulativeCompletedQty = 80m,
                    SnapshotId = 2
                }
            }
        };

        var results = await _calculator.CalculatePositionsAsync(new[] { input });
        var result = results.First();

        Assert.True(result.IsSuccess);
        Assert.Contains(result.Issues, i => i.IssueType == "DOWNSTREAM_GT_UPSTREAM");

        var s20Position = result.Positions.FirstOrDefault(p => p.StageCode == "S20");
        Assert.NotNull(s20Position);
        Assert.Equal(60m, s20Position.Quantity);
    }

    [Fact]
    public async Task F03_MissingMiddleStage_ShouldUseDownstreamMinimum()
    {
        var input = new ProductionInstructionPositionInput
        {
            ProductionInstructionNo = "PI-F03-001",
            MaterialId = 1003,
            FactoryId = 1,
            ErpRemainingQty = 100m,
            StageProgress = new[]
            {
                new StageProgressFact
                {
                    StageCode = "S10",
                    StageSequence = 1,
                    CumulativeCompletedQty = 80m,
                    SnapshotId = 1
                },
                new StageProgressFact
                {
                    StageCode = "S30",
                    StageSequence = 3,
                    CumulativeCompletedQty = 30m,
                    SnapshotId = 3
                }
            }
        };

        var results = await _calculator.CalculatePositionsAsync(new[] { input });
        var result = results.First();

        Assert.True(result.IsSuccess);
        Assert.Equal(100m, result.Positions.Sum(p => p.Quantity));

        var s10Position = result.Positions.First(p => p.StageCode == "S10");
        Assert.Equal(50m, s10Position.Quantity);

        var s30Position = result.Positions.First(p => p.StageCode == "S30");
        Assert.Equal(30m, s30Position.Quantity);
    }

    [Fact]
    public async Task F04_XcOverlapsStage_ShouldDeduplicate()
    {
        var input = new ProductionInstructionPositionInput
        {
            ProductionInstructionNo = "PI-F04-001",
            MaterialId = 1004,
            FactoryId = 1,
            ErpRemainingQty = 100m,
            StageProgress = new[]
            {
                new StageProgressFact
                {
                    StageCode = "S10",
                    StageSequence = 1,
                    CumulativeCompletedQty = 80m,
                    SnapshotId = 1
                },
                new StageProgressFact
                {
                    StageCode = "S20",
                    StageSequence = 2,
                    CumulativeCompletedQty = 30m,
                    SnapshotId = 2
                }
            },
            XcFacts = new[]
            {
                new XcFact
                {
                    XcWarehouseCode = "XC-WAREHOUSE-01",
                    RelatedStageCode = "S10",
                    Quantity = 20m,
                    AvailableTime = DateTime.Now,
                    SourceDocument = "XC-DOC-001"
                }
            }
        };

        var results = await _calculator.CalculatePositionsAsync(new[] { input });
        var result = results.First();

        Assert.True(result.IsSuccess);
        Assert.Equal(100m, result.Positions.Sum(p => p.Quantity));

        var s10Position = result.Positions.FirstOrDefault(p => p.PositionType == PositionType.STAGE && p.StageCode == "S10");
        Assert.NotNull(s10Position);
        Assert.Equal(30m, s10Position.Quantity);

        var xcPosition = result.Positions.First(p => p.PositionType == PositionType.XC);
        Assert.Equal(20m, xcPosition.Quantity);
    }

    [Fact]
    public async Task F05_TransitIndependent_ShouldNotOverlapStage()
    {
        var input = new ProductionInstructionPositionInput
        {
            ProductionInstructionNo = "PI-F05-001",
            MaterialId = 1005,
            FactoryId = 1,
            ErpRemainingQty = 100m,
            StageProgress = new[]
            {
                new StageProgressFact
                {
                    StageCode = "S10",
                    StageSequence = 1,
                    CumulativeCompletedQty = 60m,
                    SnapshotId = 1
                }
            },
            TransitFacts = new[]
            {
                new InterplantTransitFact
                {
                    SourceFactoryCode = "CN",
                    TargetFactoryCode = "BJ",
                    Quantity = 25m,
                    EstimatedArrivalTime = DateTime.Now.AddDays(2),
                    TransitDocumentNo = "TRANSIT-001"
                }
            }
        };

        var results = await _calculator.CalculatePositionsAsync(new[] { input });
        var result = results.First();

        Assert.True(result.IsSuccess);
        Assert.Equal(100m, result.Positions.Sum(p => p.Quantity));

        var transitPosition = result.Positions.First(p => p.PositionType == PositionType.INTERPLANT_IN_TRANSIT);
        Assert.Equal(25m, transitPosition.Quantity);

        var stagePosition = result.Positions.First(p => p.PositionType == PositionType.STAGE);
        Assert.Equal(60m, stagePosition.Quantity);
    }

    [Fact]
    public async Task F06_UnlocatedGap_ShouldFillWithUnlocated()
    {
        var input = new ProductionInstructionPositionInput
        {
            ProductionInstructionNo = "PI-F06-001",
            MaterialId = 1006,
            FactoryId = 1,
            ErpRemainingQty = 100m,
            StageProgress = new[]
            {
                new StageProgressFact
                {
                    StageCode = "S10",
                    StageSequence = 1,
                    CumulativeCompletedQty = 85m,
                    SnapshotId = 1
                }
            }
        };

        var results = await _calculator.CalculatePositionsAsync(new[] { input });
        var result = results.First();

        Assert.True(result.IsSuccess);
        Assert.Equal(100m, result.Positions.Sum(p => p.Quantity));

        var unlocatedPosition = result.Positions.First(p => p.PositionType == PositionType.UNLOCATED);
        Assert.Equal(15m, unlocatedPosition.Quantity);
        Assert.True(unlocatedPosition.IsUnlocated);

        Assert.Contains(result.Issues, i => i.IssueType == "UNLOCATED_GAP");
    }

    [Fact]
    public async Task F07_StrongFactCorrection_ShouldAdjustPosition()
    {
        var input = new ProductionInstructionPositionInput
        {
            ProductionInstructionNo = "PI-F07-001",
            MaterialId = 1007,
            FactoryId = 1,
            ErpRemainingQty = 100m,
            StageProgress = new[]
            {
                new StageProgressFact
                {
                    StageCode = "S10",
                    StageSequence = 1,
                    CumulativeCompletedQty = 80m,
                    SnapshotId = 1
                },
                new StageProgressFact
                {
                    StageCode = "S20",
                    StageSequence = 2,
                    CumulativeCompletedQty = 40m,
                    SnapshotId = 2
                }
            },
            StrongFacts = new[]
            {
                new ReceivedFact
                {
                    RelatedStageCode = "S10",
                    Quantity = 30m,
                    ReceivedAt = DateTime.Now,
                    DocumentNo = "RECEIVED-001"
                }
            }
        };

        var results = await _calculator.CalculatePositionsAsync(new[] { input });
        var result = results.First();

        Assert.True(result.IsSuccess);
        Assert.Equal(100m, result.Positions.Sum(p => p.Quantity));

        var s10Position = result.Positions.FirstOrDefault(p => p.PositionType == PositionType.STAGE && p.StageCode == "S10");
        if (s10Position != null)
        {
            Assert.Equal(10m, s10Position.Quantity);
        }
    }

    [Fact]
    public async Task F08_MultiplePiSameMaterial_ShouldReturnSeparateResults()
    {
        var inputs = new[]
        {
            new ProductionInstructionPositionInput
            {
                ProductionInstructionNo = "PI-F08-001",
                MaterialId = 1008,
                FactoryId = 1,
                ErpRemainingQty = 100m,
                StageProgress = new[]
                {
                    new StageProgressFact
                    {
                        StageCode = "S10",
                        StageSequence = 1,
                        CumulativeCompletedQty = 80m,
                        SnapshotId = 1
                    }
                }
            },
            new ProductionInstructionPositionInput
            {
                ProductionInstructionNo = "PI-F08-002",
                MaterialId = 1008,
                FactoryId = 1,
                ErpRemainingQty = 50m,
                StageProgress = new[]
                {
                    new StageProgressFact
                    {
                        StageCode = "S10",
                        StageSequence = 1,
                        CumulativeCompletedQty = 30m,
                        SnapshotId = 2
                    }
                }
            }
        };

        var results = await _calculator.CalculatePositionsAsync(inputs);

        Assert.Equal(2, results.Count);

        var result1 = results.First(r => r.ProductionInstructionNo == "PI-F08-001");
        Assert.True(result1.IsSuccess);
        Assert.Equal(100m, result1.Positions.Sum(p => p.Quantity));

        var result2 = results.First(r => r.ProductionInstructionNo == "PI-F08-002");
        Assert.True(result2.IsSuccess);
        Assert.Equal(50m, result2.Positions.Sum(p => p.Quantity));
    }

    // ============================================================================
    // P0-11警告：以下F09-F12测试属于跨厂订单链（INTER_FACTORY_ORDER），不属于PI Position Calculator
    //
    // 根据APS_V1_5号位代码第一轮综合符合性审核报告_冻结基线核对版_v1.0_20260820：
    // - F09-F12涉及SH（出荷指示）Transit和Received的同单号扣减逻辑
    // - SH内部Transit/Received不属于PI Position Calculator职责
    // - 这些测试需要迁移到独立的跨厂订单链测试类中
    // - 当前ProductionInstructionPositionCalculator已移除F10-F12逻辑
    //
    // 整改要求：
    // 1. 创建新测试类 InterFactoryOrderCalculatorTests 或类似名称
    // 2. 迁移F09-F12测试到该新类中
    // 3. 按跨厂订单链的正确业务语义重新设计测试
    // 4. 不新增2↔5接口字段
    // ============================================================================

    [Fact]
    public async Task F09_StageHandoff_ShouldReturnPITransitWithoutShippingTask()
    {
        var input = new ProductionInstructionPositionInput
        {
            ProductionInstructionNo = "PI-F09-001",
            MaterialId = 3001,
            FactoryId = 1,
            ErpRemainingQty = 100m,
            StageProgress = new[]
            {
                new StageProgressFact
                {
                    StageCode = "S10",
                    StageSequence = 1,
                    CumulativeCompletedQty = 50m,
                    SnapshotId = 1
                }
            },
            TransitFacts = new[]
            {
                new InterplantTransitFact
                {
                    TransitDocumentNo = "TRANSIT-001",
                    SourceFactoryCode = "CN",
                    TargetFactoryCode = "TJ",
                    Quantity = 50m,
                    SourceDocument = "SH-20260819-001",
                    ShippedAt = DateTime.Now.AddDays(-1)
                }
            }
        };

        var results = await _calculator.CalculatePositionsAsync(new[] { input });
        var result = results.First();

        Assert.True(result.IsSuccess);
        Assert.Equal(100m, result.Positions.Sum(p => p.Quantity));

        var transitPosition = result.Positions.FirstOrDefault(p => p.PositionType == PositionType.INTERPLANT_IN_TRANSIT);
        Assert.NotNull(transitPosition);
        Assert.Equal(50m, transitPosition.Quantity);

        var stagePosition = result.Positions.FirstOrDefault(p => p.StageCode == "S10");
        Assert.NotNull(stagePosition);
        Assert.Equal(50m, stagePosition.Quantity);
    }

    [Fact]
    public async Task F10_SameSHReceived_ShouldMatchCorrectly()
    {
        var input = new ProductionInstructionPositionInput
        {
            ProductionInstructionNo = "PI-F10-001",
            MaterialId = 3002,
            FactoryId = 2,
            ErpRemainingQty = 100m,
            TransitFacts = new[]
            {
                new InterplantTransitFact
                {
                    TransitDocumentNo = "TRANSIT-002",
                    SourceFactoryCode = "CN",
                    TargetFactoryCode = "TJ",
                    Quantity = 60m,
                    SourceDocument = "SH-20260819-002",
                    ShippedAt = DateTime.Now.AddDays(-2)
                }
            },
            StrongFacts = new[]
            {
                new ReceivedFact
                {
                    DocumentNo = "SH-20260819-002",
                    DocumentType = "SH",
                    Quantity = 30m,
                    ReceivedAt = DateTime.Now,
                    WarehouseCode = "TJ-WH01",
                    RelatedStageCode = "S10"
                }
            },
            StageProgress = new[]
            {
                new StageProgressFact
                {
                    StageCode = "S10",
                    StageSequence = 1,
                    CumulativeCompletedQty = 30m,
                    SnapshotId = 1
                }
            }
        };

        var results = await _calculator.CalculatePositionsAsync(new[] { input });
        var result = results.First();

        Assert.True(result.IsSuccess);
        Assert.Equal(100m, result.Positions.Sum(p => p.Quantity));

        // Transit 60 - Received 30 = 30剩余在途
        var transitPosition = result.Positions.FirstOrDefault(p => p.PositionType == PositionType.INTERPLANT_IN_TRANSIT);
        Assert.NotNull(transitPosition);
        Assert.Equal(30m, transitPosition.Quantity);

        // Stage 30被Received校正后变为0，被移除
        var stagePosition = result.Positions.FirstOrDefault(p => p.StageCode == "S10");
        Assert.True(stagePosition == null || stagePosition.Quantity < 0.01m);

        // 差额70进入UNLOCATED
        var unlocatedPosition = result.Positions.FirstOrDefault(p => p.PositionType == PositionType.UNLOCATED);
        Assert.NotNull(unlocatedPosition);
        Assert.Equal(70m, unlocatedPosition.Quantity);
    }

    [Fact]
    public async Task F11_DifferentSHSameMaterial_ShouldNotMix()
    {
        var input = new ProductionInstructionPositionInput
        {
            ProductionInstructionNo = "PI-F11-001",
            MaterialId = 3003,
            FactoryId = 2,
            ErpRemainingQty = 100m,
            TransitFacts = new[]
            {
                new InterplantTransitFact
                {
                    TransitDocumentNo = "TRANSIT-003A",
                    SourceFactoryCode = "CN",
                    TargetFactoryCode = "TJ",
                    Quantity = 40m,
                    SourceDocument = "SH-20260819-003A",
                    ShippedAt = DateTime.Now.AddDays(-2)
                },
                new InterplantTransitFact
                {
                    TransitDocumentNo = "TRANSIT-003B",
                    SourceFactoryCode = "CN",
                    TargetFactoryCode = "TJ",
                    Quantity = 60m,
                    SourceDocument = "SH-20260819-003B",
                    ShippedAt = DateTime.Now.AddDays(-1)
                }
            },
            StrongFacts = new[]
            {
                new ReceivedFact
                {
                    DocumentNo = "SH-20260819-003A",
                    DocumentType = "SH",
                    Quantity = 40m,
                    ReceivedAt = DateTime.Now,
                    WarehouseCode = "TJ-WH01",
                    RelatedStageCode = "S10"
                }
            },
            StageProgress = new[]
            {
                new StageProgressFact
                {
                    StageCode = "S10",
                    StageSequence = 1,
                    CumulativeCompletedQty = 40m,
                    SnapshotId = 1
                }
            }
        };

        var results = await _calculator.CalculatePositionsAsync(new[] { input });
        var result = results.First();

        Assert.True(result.IsSuccess);
        Assert.Equal(100m, result.Positions.Sum(p => p.Quantity));

        // Transit-003B完全保留（不同SH不串用）
        var transitPosition = result.Positions.FirstOrDefault(p => p.PositionType == PositionType.INTERPLANT_IN_TRANSIT);
        Assert.NotNull(transitPosition);
        Assert.Equal(60m, transitPosition.Quantity);

        // Stage 40被Received完全消耗后变为0
        var stagePosition = result.Positions.FirstOrDefault(p => p.StageCode == "S10");
        Assert.True(stagePosition == null || stagePosition.Quantity < 0.01m);
    }

    [Fact]
    public async Task F12_TransitAlreadyReceived_ShouldNotDuplicate()
    {
        var input = new ProductionInstructionPositionInput
        {
            ProductionInstructionNo = "PI-F12-001",
            MaterialId = 3004,
            FactoryId = 2,
            ErpRemainingQty = 100m,
            TransitFacts = new[]
            {
                new InterplantTransitFact
                {
                    TransitDocumentNo = "TRANSIT-004",
                    SourceFactoryCode = "CN",
                    TargetFactoryCode = "TJ",
                    Quantity = 50m,
                    SourceDocument = "SH-20260819-004",
                    ShippedAt = DateTime.Now.AddDays(-3)
                }
            },
            StrongFacts = new[]
            {
                new ReceivedFact
                {
                    DocumentNo = "SH-20260819-004",
                    DocumentType = "SH",
                    Quantity = 50m,
                    ReceivedAt = DateTime.Now,
                    WarehouseCode = "TJ-WH01",
                    RelatedStageCode = "S10"
                }
            },
            StageProgress = new[]
            {
                new StageProgressFact
                {
                    StageCode = "S10",
                    StageSequence = 1,
                    CumulativeCompletedQty = 50m,
                    SnapshotId = 1
                }
            }
        };

        var results = await _calculator.CalculatePositionsAsync(new[] { input });
        var result = results.First();

        Assert.True(result.IsSuccess);
        Assert.Equal(100m, result.Positions.Sum(p => p.Quantity));

        // Transit已完全Received，不再计入Position
        var transitPosition = result.Positions.FirstOrDefault(p => p.PositionType == PositionType.INTERPLANT_IN_TRANSIT);
        Assert.True(transitPosition == null || transitPosition.Quantity < 0.01m);

        // Stage 50被Received完全消耗后变为0
        var stagePosition = result.Positions.FirstOrDefault(p => p.StageCode == "S10");
        Assert.True(stagePosition == null || stagePosition.Quantity < 0.01m);

        // 差额100进入UNLOCATED
        var unlocatedPosition = result.Positions.FirstOrDefault(p => p.PositionType == PositionType.UNLOCATED);
        Assert.NotNull(unlocatedPosition);
        Assert.Equal(100m, unlocatedPosition.Quantity);
    }
}

