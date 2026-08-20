# 阶段C（Procurement/VMI）实施完成报告

**实施日期**：2026-08-20  
**实施依据**：0号位审核口径（文档/20260818/未命名的Markdown文件 (27).md）  
**实施状态**：✅ 核心计算器与测试已完成  
**测试结果**：10/10通过

---

## 一、已交付成果

### 1. DTO体系（严格遵循冻结标准）

**正式2↔5接口DTO**（`LPS.APS.Core/Dto`）：
- `TimedSupplyFact.cs` — 标准事实DTO，字段严格沿用冻结实施包，不包含ManualETA/ErpETA
- `SupplyFactScope.cs` — 查询作用域
- `FrozenFactParameters.cs` — 冻结参数快照

**内部计算输入模型**（`LPS.APS.BusinessRules/Models`）：
- `RawProcurementFact.cs` — Calculator内部输入，包含ManualETA/ErpETA/ReleaseDate等优先级计算所需字段

### 2. Calculator实现（`LPS.APS.BusinessRules/Calculators`）

`TimedSupplyFactCalculator.cs` — 核心业务逻辑：
- **F13**：Manual ETA > ERP ETA 优先级
- **F14**：ManualETA=null视为取消，回退ERP ETA（V1简化方案）
- **F15**：默认ETA = **PO Release Date + DefaultLT**（已修正，不是DataCutoffTime）
- **F16**：逾期容差，使用统一referenceTime
- **F17**：到货可用偏移（Warehouse → 小时）
- **F18**：VMI保持独立SupplyType，按真实AvailableTime消费
- **F20**：真实空返回真实空，不生成Placeholder

### 3. Fixture测试（`LPS.APS.BusinessRules.Tests/Calculators`）

`TimedSupplyFactCalculatorTests.cs` — 10个测试场景全部通过：
- F13_ManualETA_ShouldTakePriority ✅
- F14_ManualETA_CancelledShouldFallbackToErpETA ✅
- F15_MissingErpETA_ShouldUseReleaseDatePlusDefaultLT ✅
- F16_OverdueDefaultETA_ShouldApplyMargin ✅
- F16_OverdueDefaultETA_MarginBringsItCurrent_ShouldUseMarginedETA ✅
- F17_ArrivedSupply_ShouldIncludeWarehouseOffset ✅
- F17_WarehouseWithNoOffset_ShouldUseETADirectly ✅
- F18_VMI_ShouldBeIndependentSupplyType ✅
- F20_EmptySupply_ShouldReturnEmptyNotPlaceholder ✅
- Integration_ManualETA_OverridesEverything ✅

---

## 二、严格遵循0号位审核的14条口径

### ✅ 已执行的关键修正

1. **F15基准日期已修正**：`PO Release/Issue Date + DefaultLT`，不是`DataCutoffTime + DefaultLT`
2. **TimedSupplyFact正式DTO不扩张**：ManualETA/ErpETA作为内部RawProcurementFact，不暴露到2↔5接口
3. **F19未被改动**：本阶段只实现F13-F18、F20，F19保持为"ProcessCode.ERPProperty"，在阶段D完成
4. **人工ETA取消简化**：V1采用ManualETA=null即为取消，不新建ManualETAOverride历史表
5. **VMI不预设规则**：按真实标准事实的AvailableTime消费，不新增"所有VMI立即可用"规则
6. **Placeholder职责边界**：5号位只返回真实事实，F20验收通过
7. **未修改2号位代码**：未触碰PeggingOrchestrator.cs，未修改PeggingExecutionRequest
8. **未新建第二套Service接口**：未创建ITimedSupplyFactService

### ✅ 字段与枚举使用冻结值域

- SupplyType使用冻结值域：`OPEN_PO_REMAINING`, `VMI_ONSITE`, `ARRIVED_NOT_RECEIVED`, `PURCHASE_IN_TRANSIT`
- 未新增同义枚举（如PURCHASE_ORDER ↔ OPEN_PO_REMAINING）

---

## 三、当前未完成项（按0号位口径，这些不属于阶段C范围）

### 1. ODS数据源对接（阶段D）

当前Calculator使用Fixture测试，未对接真实ODS：
- `ext_PipelineSupply_Source_View` → `SupplyFact_Pipeline` 链未接入
- 不直接假定`ERP_PurchaseOrder_View` / `ERP_VMI_View`等具体名称
- 按既有ODS契约绑定

### 2. 与2号位集成（阶段D）

以下集成工作留待2号位联调：
- FrozenFactParameters传递路径（从SchedulingContext投影）
- LoadSupplyPoolAsync扩展（装载TimedSupplyFact）
- 验证SupplySourceType枚举完整性

### 3. F19（阶段D）

F19正式验收："ProcessCode.ERPProperty必须来自ERP真实属性"，在阶段D完成。

---

## 四、技术债务与改进建议

### 1. RawProcurementFact可访问性

当前`RawProcurementFact`是`public`，因为Calculator是`public`方法参数。
- **建议**：等阶段D集成时，如果只有内部调用，可改为`internal`

### 2. F16统一参考时间

当前实现要求调用方传入`referenceTime`参数，避免逐条`DateTime.Now`漂移。
- **建议**：阶段D集成时，从`SupplyFactScope.DataCutoffTime`自动取值

### 3. Warehouse Offset配置来源

当前`ArrivalToUsableOffsets`是Dictionary<string, int>。
- **建议**：阶段D确认是否需要独立配置表（如`WarehouseOffsetConfig`）

---

## 五、验收标准达成情况

| 场景 | 验收标准 | 状态 |
|-----|---------|------|
| F13 | Manual ETA > ERP ETA | ✅ 通过 |
| F14 | 取消后回退ERP ETA | ✅ 通过（V1简化） |
| F15 | Release Date + DefaultLT | ✅ 通过（已修正基准） |
| F16 | 逾期容差 | ✅ 通过（统一referenceTime） |
| F17 | Warehouse Offset | ✅ 通过 |
| F18 | VMI独立类型 | ✅ 通过 |
| F19 | ERPProperty | ⏸️ 阶段D（未改动） |
| F20 | 真实空返回空 | ✅ 通过 |

---

## 六、下一步行动（阶段D）

### 等待2号位联调时执行：

1. **验证SupplySourceType枚举**：确认是否包含`PURCHASE_IN_TRANSIT` / `OPEN_PO_REMAINING` / `ARRIVED_NOT_RECEIVED` / `VMI_ONSITE`
2. **确认FrozenFactParameters传递路径**：从SchedulingContext投影还是扩展PeggingExecutionRequest
3. **扩展LoadSupplyPoolAsync**：装载TimedSupplyFact并加入SupplyPool
4. **ODS契约对接**：按`ext_PipelineSupply_Source_View → SupplyFact_Pipeline`链接入真实采购/VMI数据
5. **F19验收**：ERPProperty必须来自ERP真实属性

### 性能测试（阶段D）

- 批量加载性能（1000+ PO）
- ETA计算性能（多优先级判断）
- Warehouse Offset查找性能

---

## 七、总结

阶段C核心目标**已完成**：
- ✅ TimedSupplyFact DTO体系（严格遵循冻结标准）
- ✅ TimedSupplyFactCalculator实现（F13-F18、F20）
- ✅ Fixture测试全部通过（10/10）
- ✅ 未触碰2号位代码
- ✅ 未新建第二套Service接口
- ✅ F15基准日期已修正为PO Release Date

**按0号位口径，阶段C可以验收。**

下一步等待2号位返岗联调，进入阶段D（ODS真实数据对接 + F19 ERPProperty验收）。

---

**报告完成**  
**2026-08-20**
