using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Larpx.PersonalTools.TypeU.Network.Discovery;
using Larpx.PersonalTools.TypeU.Network.Messages;
using Larpx.PersonalTools.TypeU.Network.Tcp;
using Microsoft.Extensions.Logging;

namespace Larpx.PersonalTools.TypeU.Services.Student;

/// <summary>
/// 学生端自动发现服务：监听 UDP 广播自动发现教师端并连接；超时未发现时触发手动输入教师端 IP 的兜底流程。
/// </summary>
public sealed class StudentDiscoveryService : IDisposable
{
    private readonly UdpDiscoveryListener _listener;
    private readonly TcpExamClient _client;
    private readonly ILogger<StudentDiscoveryService>? _logger;
    private readonly TimeSpan _discoveryTimeout;
    private CancellationTokenSource? _discoverCts;

    /// <summary>
    /// 自动发现教师端成功事件（参数：教师端 IP、端口）。
    /// </summary>
    public event Action<IPAddress, int>? TeacherDiscovered;

    /// <summary>
    /// 自动发现超时事件（参数：是否仍未发现）。
    /// </summary>
    public event Action? DiscoveryTimeout;

    /// <summary>
    /// 已成功连接教师端事件。
    /// </summary>
    public event Action? Connected;

    /// <summary>
    /// 初始化服务。
    /// </summary>
    /// <param name="listener">UDP 发现监听器。</param>
    /// <param name="client">TCP 客户端。</param>
    /// <param name="discoveryTimeoutSeconds">自动发现超时秒数（默认 5 秒）。</param>
    /// <param name="logger">日志。</param>
    public StudentDiscoveryService(
        UdpDiscoveryListener listener,
        TcpExamClient client,
        int discoveryTimeoutSeconds = 5,
        ILogger<StudentDiscoveryService>? logger = null)
    {
        _listener = listener ?? throw new ArgumentNullException(nameof(listener));
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _discoveryTimeout = TimeSpan.FromSeconds(discoveryTimeoutSeconds);
        _logger = logger;
        _listener.BroadcastReceived += OnBroadcastReceived;
    }

    /// <summary>
    /// 启动自动发现流程。若在超时内收到广播则自动连接；否则触发 DiscoveryTimeout 事件。
    /// </summary>
    public async Task StartDiscoveryAsync()
    {
        _listener.Start();
        _discoverCts?.Dispose();
        _discoverCts = new CancellationTokenSource(_discoveryTimeout);
        _logger?.LogInformation("自动发现已启动，超时 {Sec}s", _discoveryTimeout.TotalSeconds);

        try
        {
            await Task.Delay(_discoveryTimeout, _discoverCts.Token).ConfigureAwait(false);
            // 超时未发现：触发兜底流程。
            _listener.Stop();
            _logger?.LogWarning("自动发现超时，未收到教师端广播");
            DiscoveryTimeout?.Invoke();
        }
        catch (OperationCanceledException)
        {
            // 已在超时内发现教师端，无需处理。
        }
    }

    /// <summary>
    /// 手动输入教师端 IP 时调用（兜底流程）。
    /// </summary>
    public async Task ManuallyConnectAsync(string host, int port, bool autoReconnect = true)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            throw new ArgumentException("教师端 IP 不能为空。", nameof(host));
        }

        _discoverCts?.Cancel();
        _listener.Stop();
        await _client.ConnectAsync(host, port, autoReconnect).ConfigureAwait(false);
        _logger?.LogInformation("已手动连接教师端：{Host}:{Port}", host, port);
        Connected?.Invoke();
    }

    private async void OnBroadcastReceived(DiscoveryBroadcastMessage message, IPEndPoint remote)
    {
        if (_discoverCts is null || _discoverCts.IsCancellationRequested)
        {
            return;
        }

        // 取消超时任务。
        _discoverCts.Cancel();
        _listener.Stop();

        var teacherIp = remote.Address;
        var teacherPort = message.TeacherPort;
        _logger?.LogInformation("发现教师端：{Ip}:{Port}（{Name}）",
            teacherIp, teacherPort, message.TeacherName);
        TeacherDiscovered?.Invoke(teacherIp, teacherPort);

        try
        {
            await _client.ConnectAsync(teacherIp.ToString(), teacherPort, autoReconnect: true)
                .ConfigureAwait(false);
            Connected?.Invoke();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "自动连接教师端失败");
            DiscoveryTimeout?.Invoke();
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _listener.BroadcastReceived -= OnBroadcastReceived;
        _listener.Dispose();
        _discoverCts?.Dispose();
    }
}
