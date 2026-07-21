using System;
using ProtoBuf;

namespace Larpx.PersonalTools.TypeU.Network.Messages;

/// <summary>
/// 时间同步消息（教师端 10 秒广播一次，作为考试倒计时唯一基准）。
/// </summary>
[ProtoContract]
public sealed partial class TimeSyncMessage
{
    /// <summary>
    /// 教师端当前 UTC 时间（毫秒，自 1970-01-01 起计）。
    /// </summary>
    [ProtoMember(1)]
    public long TeacherTimestampMs { get; set; }

    /// <summary>
    /// 当前会话 ID（未开考时为 Guid.Empty）。
    /// </summary>
    [ProtoMember(2)]
    public Guid SessionId { get; set; }

    /// <summary>
    /// 考试剩余秒数（未开考时为 0；暂停时为暂停瞬间剩余秒数）。
    /// </summary>
    [ProtoMember(3)]
    public int RemainingSeconds { get; set; }

    /// <summary>
    /// 考试状态：0=空闲, 1=进行中, 2=暂停, 3=已结束。
    /// </summary>
    [ProtoMember(4)]
    public int ExamState { get; set; }
}
