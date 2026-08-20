namespace LPS.APS.Core.Enum;

/// <summary>
/// 生产指示位置类型
/// 定义PI RemainingQty当前所处的位置状态
/// </summary>
public enum PositionType
{
    /// <summary>
    /// Stage位置（在某个大工艺阶段）
    /// </summary>
    STAGE = 1,

    /// <summary>
    /// XC位置（线边仓，半成品临时存储）
    /// </summary>
    XC = 2,

    /// <summary>
    /// 厂间在途（已从上游工厂发出，尚未到达目标工厂）
    /// </summary>
    INTERPLANT_IN_TRANSIT = 3,

    /// <summary>
    /// 等待状态（已投料但尚未进入实际加工）
    /// </summary>
    WAITING = 4,

    /// <summary>
    /// 无法定位（位置不明确，但总量必须闭合）
    /// 2号位会按保守策略从最早Stage开始形成计划需求
    /// </summary>
    UNLOCATED = 5
}
