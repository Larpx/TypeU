namespace Larpx.PersonalTools.TypeU.Models.Enums;

/// <summary>
/// 考试模式。
/// </summary>
public enum ExamMode
{
    /// <summary>
    /// 定篇测速：固定文本，比拼速度与正确率。
    /// </summary>
    FixedLength = 0,

    /// <summary>
    /// 限时冲刺：限定时长（1-20 分钟），统计完成量。
    /// </summary>
    TimedSprint = 1,

    /// <summary>
    /// 纠错模式：下发含错原文，学生输入修正后的正确文本并计分。
    /// </summary>
    ErrorCorrection = 2
}
