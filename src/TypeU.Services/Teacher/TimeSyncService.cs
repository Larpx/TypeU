using System;
using System.Threading;
using System.Threading.Tasks;
using Larpx.PersonalTools.TypeU.Network.Messages;
using Larpx.PersonalTools.TypeU.Network.Protocol;
using Larpx.PersonalTools.TypeU.Network.Security;
using Larpx.PersonalTools.TypeU.Network.Tcp;
using Microsoft.Extensions.Logging;
using ProtoBuf;

namespace Larpx.PersonalTools.TypeU.Services.Teacher;

/// <summary>
/// 教师端时间同步服务：每 10 秒向所有在线学生端广播教师端时间。
/// 学生端按教师端时间计算考试剩余时长，防止本地改时间作弊。
/// </summary>
public sealed class TimeSyncService
{
    private readonly TcpExamServer _server;
    private readonly ILogger<TimeSyncService>? _logger;
    private readonly TimeSpan _interval;
    private CancellationTokenSource? _cts;
    private Task? _loopTask;
    private Guid _sessionId;
    private int _remainingSeconds;
    private int _examState;

    /// <summary>
    /// 初始化服务。
    /// </summary>
    /// <param name="server">TCP 服务端。</param>
    /// <param name="logger">日志。</param>
    /// <param name="interval">广播间隔（默认 10 秒）。</param>
    public TimeSyncService(TcpExamServer server, ILogger<TimeSyncService>? logger = null, TimeSpan? interval = null)
    {
        _server = server ?? throw new ArgumentNullException(nameof(server));
        _logger = logger;
        _interval = interval ?? TimeSpan.FromSeconds(10);
    }

    /// <summary>
    /// 启动定时广播。
    /// </summary>
    public void Start()
    {
        if (_cts is not null)
        {
            throw new InvalidOperationException("时间同步服务已启动。");
        }
        _cts = new CancellationTokenSource();
        _loopTask = Task.Run(() => BroadcastLoopAsync(_cts.Token), _cts.Token);
        _logger?.LogInformation("时间同步服务已启动，间隔 {Interval}s", _interval.TotalSeconds);
    }

    /// <summary>
    /// 停止广播。
    /// </summary>
    public void Stop()
    {
        if (_cts is null)
        {
            return;
        }
        _cts.Cancel();
        try { _loopTask?.Wait(TimeSpan.FromSeconds(2)); }
        catch { /* 忽略取消异常。 */ }
        _cts.Dispose();
        _cts = null;
        _loopTask = null;
        _logger?.LogInformation("时间同步服务已停止");
    }

    /// <summary>
    /// 通知服务：考试开始（携带会话 ID 与时长）。
    /// </summary>
    public void NotifyExamStarted(Guid sessionId, int durationSeconds)
    {
        _sessionId = sessionId;
        _remainingSeconds = durationSeconds;
        _examState = 1; // 进行中。
    }

    /// <summary>
    /// 通知服务：考试暂停。
    /// </summary>
    public void NotifyExamPaused()
    {
        _examState = 2;
    }

    /// <summary>
    /// 通知服务：考试结束。
    /// </summary>
    public void NotifyExamStopped()
    {
        _examState = 3;
        _remainingSeconds = 0;
        _sessionId = Guid.Empty;
    }

    private async Task BroadcastLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (_examState == 1 && _remainingSeconds > 0)
                {
                    _remainingSeconds = Math.Max(0, _remainingSeconds - (int)_interval.TotalSeconds);
                }

                var msg = new TimeSyncMessage
                {
                    TeacherTimestampMs = TimestampValidator.NowUtcMs(),
                    SessionId = _sessionId,
                    RemainingSeconds = _remainingSeconds,
                    ExamState = _examState
                };

                using var ms = new System.IO.MemoryStream();
                Serializer.Serialize(ms, msg);
                var payload = ms.ToArray();
                await _server.BroadcastAsync(MessageType.TimeSync, payload).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "时间同步广播失败");
            }

            try
            {
                await Task.Delay(_interval, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
