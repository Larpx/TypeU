using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Larpx.PersonalTools.TypeU.Models.Dtos;
using Larpx.PersonalTools.TypeU.Network.Protocol;
using Larpx.PersonalTools.TypeU.Network.Tcp;
using Microsoft.Extensions.Logging;
using ProtoBuf;

namespace Larpx.PersonalTools.TypeU.Services.Student;

/// <summary>
/// 状态上报服务：定时（1-2 秒）上报实时速度与状态；断线时缓存到内存队列，重连后补传。
/// </summary>
public sealed class StatusReportService : IDisposable
{
    private readonly TcpExamClient _client;
    private readonly ILogger<StatusReportService>? _logger;
    private readonly ConcurrentQueue<StatusReportDto> _pendingQueue = new();
    private readonly TimeSpan _interval;
    private Timer? _timer;
    private Func<StatusReportDto>? _snapshotGetter;
    private string _studentId = string.Empty;
    private Guid _sessionId;
    private int _anomalyCount;

    /// <summary>
    /// 初始化服务。
    /// </summary>
    /// <param name="client">TCP 客户端。</param>
    /// <param name="intervalMs">上报间隔（默认 1500 毫秒）。</param>
    /// <param name="logger">日志。</param>
    public StatusReportService(
        TcpExamClient client,
        int intervalMs = 1500,
        ILogger<StatusReportService>? logger = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _interval = TimeSpan.FromMilliseconds(intervalMs);
        _logger = logger;
    }

    /// <summary>
    /// 启动定时上报。
    /// </summary>
    /// <param name="studentId">学号。</param>
    /// <param name="sessionId">会话 ID。</param>
    /// <param name="snapshotGetter">获取当前快照的回调（速度/正确率/进度/总字符数）。</param>
    public void Start(string studentId, Guid sessionId, Func<StatusReportDto> snapshotGetter)
    {
        if (string.IsNullOrWhiteSpace(studentId))
        {
            throw new ArgumentException("学号不能为空。", nameof(studentId));
        }
        _studentId = studentId;
        _sessionId = sessionId;
        _snapshotGetter = snapshotGetter ?? throw new ArgumentNullException(nameof(snapshotGetter));
        _anomalyCount = 0;
        _timer?.Dispose();
        _timer = new Timer(OnTick, null, TimeSpan.Zero, _interval);
        _logger?.LogInformation("状态上报已启动：间隔 {Ms}ms", _interval.TotalMilliseconds);
    }

    /// <summary>
    /// 停止定时上报。
    /// </summary>
    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;
        _snapshotGetter = null;
        _logger?.LogInformation("状态上报已停止");
    }

    /// <summary>
    /// 累计异常次数（用于上报时附带）。
    /// </summary>
    public void IncrementAnomaly(int delta = 1)
    {
        Interlocked.Add(ref _anomalyCount, delta);
    }

    /// <summary>
    /// 当前待补传队列长度。
    /// </summary>
    public int PendingCount => _pendingQueue.Count;

    private async void OnTick(object? state)
    {
        if (_snapshotGetter is null)
        {
            return;
        }

        StatusReportDto dto;
        try
        {
            dto = _snapshotGetter();
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "获取状态快照失败");
            return;
        }

        dto.StudentId = _studentId;
        dto.SessionId = _sessionId;
        dto.AnomalyCount = _anomalyCount;
        dto.Timestamp = DateTime.UtcNow;

        if (_client.IsConnected)
        {
            // 先补传历史缓存。
            await FlushPendingAsync().ConfigureAwait(false);
            await SendAsync(dto).ConfigureAwait(false);
        }
        else
        {
            _pendingQueue.Enqueue(dto);
            _logger?.LogDebug("断线，状态已入补传队列（当前 {Count}）", _pendingQueue.Count);
        }
    }

    /// <summary>
    /// 重连成功后立即补传全部缓存。
    /// </summary>
    public async Task FlushPendingAsync()
    {
        while (_pendingQueue.TryDequeue(out var dto))
        {
            await SendAsync(dto).ConfigureAwait(false);
        }
    }

    private async Task SendAsync(StatusReportDto dto)
    {
        try
        {
            byte[] payload;
            using (var ms = new System.IO.MemoryStream())
            {
                Serializer.Serialize(ms, dto);
                payload = ms.ToArray();
            }
            await _client.SendAsync(MessageType.StatusReport, payload).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _pendingQueue.Enqueue(dto);
            _logger?.LogWarning(ex, "状态上报失败，已入补传队列");
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Stop();
    }
}
