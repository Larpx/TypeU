using System;
using ProtoBuf;

namespace Larpx.PersonalTools.TypeU.Network.Messages;

/// <summary>
/// UDP 教师端发现广播负载。
/// </summary>
[ProtoContract]
public sealed partial class DiscoveryBroadcastMessage
{
    /// <summary>
    /// 教师端 TCP 端口。
    /// </summary>
    [ProtoMember(1)]
    public int TeacherPort { get; set; }

    /// <summary>
    /// 教师端主机名或标识（用于学生端展示）。
    /// </summary>
    [ProtoMember(2)]
    public string TeacherName { get; set; } = string.Empty;

    /// <summary>
    /// 广播时间戳（UTC 毫秒）。
    /// </summary>
    [ProtoMember(3)]
    public long TimestampMs { get; set; }
}
