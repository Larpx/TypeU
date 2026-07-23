namespace Larpx.PersonalTools.TypeU.Models.Enums;

/// <summary>
/// 学生端进入练习/考试页的模式。
/// </summary>
public enum PracticeMode
{
    /// <summary>
    /// 单机练习：未连接教师端，不上报状态、不计成绩。
    /// </summary>
    Offline,

    /// <summary>
    /// 联网练习：已连接教师端但未开考，上报状态供教师端巡视，不计成绩、不锁定退出。
    /// </summary>
    OnlinePractice,

    /// <summary>
    /// 考试登录:已连接且开考，上报状态、计成绩，锁定退出直至教师结束考试。
    /// </summary>
    Exam
}
