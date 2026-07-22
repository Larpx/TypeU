using System;
using ProtoBuf;

namespace Larpx.PersonalTools.TypeU.Models.Dtos;

/// <summary>
/// 握手应答（教师端 → 学生端）。
/// </summary>
[ProtoContract]
public sealed partial class HelloAckDto
{
    /// <summary>当前是否存在进行中的考试会话。</summary>
    [ProtoMember(1)]
    public bool ExamRunning { get; set; }

    /// <summary>当前会话 ID（未开考为空）。</summary>
    [ProtoMember(2)]
    public Guid SessionId { get; set; }

    /// <summary>是否可根据设备指纹自动登录。</summary>
    [ProtoMember(3)]
    public bool AutoLogin { get; set; }

    /// <summary>自动登录学号。</summary>
    [ProtoMember(4)]
    public string StudentId { get; set; } = string.Empty;

    /// <summary>自动登录姓名。</summary>
    [ProtoMember(5)]
    public string StudentName { get; set; } = string.Empty;

    /// <summary>是否允许登出。</summary>
    [ProtoMember(6)]
    public bool LogoutAllowed { get; set; }

    /// <summary>允许的最大测验次数。</summary>
    [ProtoMember(7)]
    public int MaxAttempts { get; set; }

    /// <summary>交卷后是否允许自由练习。</summary>
    [ProtoMember(8)]
    public bool AllowPracticeAfterSubmit { get; set; }

    /// <summary>服务器时间戳（UTC 毫秒）。</summary>
    [ProtoMember(9)]
    public long ServerTimestampMs { get; set; }
}
