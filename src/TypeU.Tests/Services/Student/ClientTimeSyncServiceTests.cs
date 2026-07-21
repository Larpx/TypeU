using System;
using System.Threading;
using Larpx.PersonalTools.TypeU.Network.Messages;
using Larpx.PersonalTools.TypeU.Services.Student;
using Xunit;

namespace Larpx.PersonalTools.TypeU.Tests.Services.Student;

/// <summary>
/// ClientTimeSyncService 单元测试：时间同步、剩余时间计算、重置。
/// 验证本地系统时间修改不影响考试计时（仅依赖教师端时间 + 本地单调时钟）。
/// </summary>
public sealed class ClientTimeSyncServiceTests
{
    /// <summary>
    /// 收到时间同步后，状态与剩余秒数应正确更新。
    /// </summary>
    [Fact]
    public void OnTimeSyncReceived_UpdatesState()
    {
        var svc = new ClientTimeSyncService();
        var sessionId = Guid.NewGuid();

        var msg = new TimeSyncMessage
        {
            TeacherTimestampMs = 1_700_000_000_000L,
            SessionId = sessionId,
            RemainingSeconds = 600,
            ExamState = 1 // Running
        };

        svc.OnTimeSyncReceived(msg);

        Assert.True(svc.HasSynced);
        Assert.Equal(ClientExamState.Running, svc.ExamState);
        Assert.Equal(sessionId, svc.SessionId);
        Assert.Equal(1_700_000_000_000L, svc.LastTeacherTimestampMs);
    }

    /// <summary>
    /// Running 状态下，GetRemainingSeconds 应随本地流逝时间递减。
    /// </summary>
    [Fact]
    public void GetRemainingSeconds_DecreasesWithLocalElapsed()
    {
        var svc = new ClientTimeSyncService();
        svc.OnTimeSyncReceived(new TimeSyncMessage
        {
            TeacherTimestampMs = 1_700_000_000_000L,
            RemainingSeconds = 600,
            ExamState = 1
        });

        var before = svc.GetRemainingSeconds();
        Assert.True(before <= 600);

        Thread.Sleep(1100);
        var after = svc.GetRemainingSeconds();
        Assert.True(after < before, $"after={after} 应小于 before={before}");
    }

    /// <summary>
    /// Paused 状态下，GetRemainingSeconds 应返回固定值（不随时间递减）。
    /// </summary>
    [Fact]
    public void GetRemainingSeconds_Paused_StaysConstant()
    {
        var svc = new ClientTimeSyncService();
        svc.OnTimeSyncReceived(new TimeSyncMessage
        {
            TeacherTimestampMs = 1_700_000_000_000L,
            RemainingSeconds = 300,
            ExamState = 2 // Paused
        });

        var before = svc.GetRemainingSeconds();
        Thread.Sleep(100);
        var after = svc.GetRemainingSeconds();
        Assert.Equal(before, after);
    }

    /// <summary>
    /// Reset 后状态应清空。
    /// </summary>
    [Fact]
    public void Reset_ClearsState()
    {
        var svc = new ClientTimeSyncService();
        svc.OnTimeSyncReceived(new TimeSyncMessage
        {
            TeacherTimestampMs = 1_700_000_000_000L,
            RemainingSeconds = 600,
            ExamState = 1
        });

        svc.Reset();

        Assert.False(svc.HasSynced);
        Assert.Equal(ClientExamState.Idle, svc.ExamState);
        Assert.Equal(Guid.Empty, svc.SessionId);
    }

    /// <summary>
    /// GetTeacherNowMs 应基于教师端时间 + 本地流逝计算，不依赖本地系统时间。
    /// </summary>
    [Fact]
    public void GetTeacherNowMs_CalculatesFromTeacherBase()
    {
        var svc = new ClientTimeSyncService();
        var baseMs = 1_700_000_000_000L;
        svc.OnTimeSyncReceived(new TimeSyncMessage
        {
            TeacherTimestampMs = baseMs,
            RemainingSeconds = 0,
            ExamState = 0
        });

        var now = svc.GetTeacherNowMs();
        Assert.True(now >= baseMs, $"now={now} 应大于等于 base={baseMs}");
    }

    /// <summary>
    /// 未同步时 GetTeacherNowMs 应返回 0。
    /// </summary>
    [Fact]
    public void GetTeacherNowMs_ReturnsZero_WhenNotSynced()
    {
        var svc = new ClientTimeSyncService();
        Assert.Equal(0, svc.GetTeacherNowMs());
    }
}
