namespace Larpx.PersonalTools.TypeU.Models.Enums;

/// <summary>
/// 考试会话状态（持久化）。
/// </summary>
public enum ExamSessionStatus
{
    /// <summary>进行中（已开考，允许登录）。</summary>
    Running = 1,

    /// <summary>已结束（学生可自行登出）。</summary>
    Ended = 2
}
