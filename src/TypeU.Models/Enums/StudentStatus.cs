namespace Larpx.PersonalTools.TypeU.Models.Enums;

/// <summary>
/// 学生在线状态。
/// </summary>
public enum StudentStatus
{
    /// <summary>
    /// 离线。
    /// </summary>
    Offline = 0,

    /// <summary>
    /// 在线空闲。
    /// </summary>
    Online = 1,

    /// <summary>
    /// 考试中。
    /// </summary>
    Examining = 2,

    /// <summary>
    /// 已提交。
    /// </summary>
    Submitted = 3,

    /// <summary>
    /// 异常告警。
    /// </summary>
    Anomaly = 4
}
