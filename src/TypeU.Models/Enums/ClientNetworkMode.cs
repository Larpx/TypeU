namespace Larpx.PersonalTools.TypeU.Models.Enums;

/// <summary>
/// 学生端联网模式（状态栏展示）。
/// </summary>
public enum ClientNetworkMode
{
    /// <summary>
    /// 正在自动发现教师端。
    /// </summary>
    Discovering = 0,

    /// <summary>
    /// 已连接教师端（联网考试）。
    /// </summary>
    Online = 1,

    /// <summary>
    /// 未找到教师端，单机练习模式。
    /// </summary>
    Offline = 2,

    /// <summary>
    /// 正在手动连接教师端。
    /// </summary>
    Connecting = 3
}
