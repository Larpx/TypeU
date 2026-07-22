namespace Larpx.PersonalTools.TypeU.Network.Protocol;

/// <summary>
/// 协议消息类型。
/// </summary>
public enum MessageType : ushort
{
    /// <summary>
    /// 未知类型。
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// 学生签到登录请求。
    /// </summary>
    Login = 1,

    /// <summary>
    /// 登录响应（教师端 → 学生端）。
    /// </summary>
    LoginAck = 2,

    /// <summary>
    /// 时间同步广播（教师端 → 学生端，10 秒一次）。
    /// </summary>
    TimeSync = 10,

    /// <summary>
    /// 试题下发（教师端 → 学生端）。
    /// </summary>
    QuestionPush = 20,

    /// <summary>
    /// 考试流程控制（开始/暂停/停止/重新考试）。
    /// </summary>
    ExamControl = 21,

    /// <summary>
    /// 学生状态实时上报（学生端 → 教师端，1-2 秒一次）。
    /// </summary>
    StatusReport = 30,

    /// <summary>
    /// 学生成绩回传（学生端 → 教师端，收卷时一次）。
    /// </summary>
    ResultSubmit = 31,

    /// <summary>
    /// 心跳保活。
    /// </summary>
    Heartbeat = 40,

    /// <summary>
    /// 心跳响应。
    /// </summary>
    HeartbeatAck = 41,

    /// <summary>
    /// 通用错误响应。
    /// </summary>
    Error = 50,

    /// <summary>
    /// UDP 教师端发现广播。
    /// </summary>
    DiscoveryBroadcast = 100,

    /// <summary>
    /// 局域网扫描请求（教师端 → 子网广播，识别未装学生端的设备）。
    /// </summary>
    LanScanRequest = 101,

    /// <summary>
    /// 局域网扫描响应（被扫描设备 → 教师端）。
    /// </summary>
    LanScanResponse = 102,

    /// <summary>
    /// 连接握手（学生端 → 教师端）。
    /// </summary>
    Hello = 110,

    /// <summary>
    /// 握手应答（教师端 → 学生端）。
    /// </summary>
    HelloAck = 111,

    /// <summary>
    /// 学生主动登出。
    /// </summary>
    Logout = 112,

    /// <summary>
    /// 教师允许指定学生登出。
    /// </summary>
    LogoutAllow = 113,

    /// <summary>
    /// 考试生命周期（开考/结束广播）。
    /// </summary>
    ExamLifecycle = 114
}
