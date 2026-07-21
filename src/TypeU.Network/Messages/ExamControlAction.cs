namespace Larpx.PersonalTools.TypeU.Network.Messages;

/// <summary>
/// 考试流程控制动作。
/// </summary>
public enum ExamControlAction
{
    /// <summary>
    /// 开始考试。
    /// </summary>
    Start = 0,

    /// <summary>
    /// 暂停。
    /// </summary>
    Pause = 1,

    /// <summary>
    /// 恢复。
    /// </summary>
    Resume = 2,

    /// <summary>
    /// 停止（收卷）。
    /// </summary>
    Stop = 3,

    /// <summary>
    /// 重新考试（重置学生端状态与本地草稿）。
    /// </summary>
    Restart = 4
}
