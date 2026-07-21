using System;
using System.Threading;
using Larpx.PersonalTools.TypeU.Network.Messages;
using Microsoft.Extensions.Logging;

namespace Larpx.PersonalTools.TypeU.Services.Student;

/// <summary>
/// 考试状态枚举（与 TimeSyncMessage.ExamState 对应）。
/// </summary>
public enum ClientExamState
{
    /// <summary>空闲。</summary>
    Idle = 0,

    /// <summary>进行中。</summary>
    Running = 1,

    /// <summary>暂停。</summary>
    Paused = 2,

    /// <summary>已结束。</summary>
    Ended = 3
}

/// <summary>
/// 学生端时间同步服务：接收教师端时间广播，按教师端时间计算考试剩余时长。
/// 完全忽略本地系统时间，防止本地修改时间作弊。
/// </summary>
public sealed class ClientTimeSyncService
{
    private readonly ILogger<ClientTimeSyncService>? _logger;
    private long _lastTeacherTimestampMs;
    private long _lastLocalTicks;
    private int _remainingSeconds;
    private ClientExamState _examState = ClientExamState.Idle;
    private Guid _sessionId;

    /// <summary>
    /// 初始化服务。
    /// </summary>
    public ClientTimeSyncService(ILogger<ClientTimeSyncService>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// 最近一次教师端时间戳（UTC 毫秒）。
    /// </summary>
    public long LastTeacherTimestampMs => Interlocked.Read(ref _lastTeacherTimestampMs);

    /// <summary>
    /// 当前考试状态。
    /// </summary>
    public ClientExamState ExamState => _examState;

    /// <summary>
    /// 当前会话 ID。
    /// </summary>
    public Guid SessionId => _sessionId;

    /// <summary>
    /// 最近一次时间同步的本地接收时刻（Environment.TickCount64）。
    /// </summary>
    public long LastLocalTicks => Interlocked.Read(ref _lastLocalTicks);

    /// <summary>
    /// 是否已收到过至少一次教师端时间同步。
    /// </summary>
    public bool HasSynced => Interlocked.Read(ref _lastTeacherTimestampMs) > 0;

    /// <summary>
    /// 处理收到的 TimeSyncMessage。
    /// </summary>
    public void OnTimeSyncReceived(TimeSyncMessage message)
    {
        if (message is null)
        {
            throw new ArgumentNullException(nameof(message));
        }

        Interlocked.Exchange(ref _lastTeacherTimestampMs, message.TeacherTimestampMs);
        Interlocked.Exchange(ref _lastLocalTicks, Environment.TickCount64);
        _remainingSeconds = message.RemainingSeconds;
        _examState = (ClientExamState)message.ExamState;
        _sessionId = message.SessionId;
        _logger?.LogDebug("时间同步：教师端 {Ms}ms，状态 {State}，剩余 {Rem}s",
            message.TeacherTimestampMs, _examState, _remainingSeconds);
    }

    /// <summary>
    /// 基于教师端时间 + 本地流逝计算当前剩余秒数。
    /// 暂停状态下返回暂停瞬间剩余秒数；空闲/已结束返回 0。
    /// </summary>
    public int GetRemainingSeconds()
    {
        if (_examState != ClientExamState.Running)
        {
            return _remainingSeconds;
        }

        var elapsedMs = Environment.TickCount64 - Interlocked.Read(ref _lastLocalTicks);
        var elapsedSec = (int)(elapsedMs / 1000);
        return Math.Max(0, _remainingSeconds - elapsedSec);
    }

    /// <summary>
    /// 获取当前教师端时间（UTC 毫秒）。
    /// </summary>
    public long GetTeacherNowMs()
    {
        var baseMs = Interlocked.Read(ref _lastTeacherTimestampMs);
        if (baseMs == 0)
        {
            return 0;
        }
        var elapsedMs = Environment.TickCount64 - Interlocked.Read(ref _lastLocalTicks);
        return baseMs + elapsedMs;
    }

    /// <summary>
    /// 重置状态（重新考试时调用）。
    /// </summary>
    public void Reset()
    {
        Interlocked.Exchange(ref _lastTeacherTimestampMs, 0);
        Interlocked.Exchange(ref _lastLocalTicks, 0);
        _remainingSeconds = 0;
        _examState = ClientExamState.Idle;
        _sessionId = Guid.Empty;
    }
}
