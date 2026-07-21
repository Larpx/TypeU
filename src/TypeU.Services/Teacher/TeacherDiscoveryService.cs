using System;
using System.Threading;
using System.Threading.Tasks;
using Larpx.PersonalTools.TypeU.Network.Discovery;
using Larpx.PersonalTools.TypeU.Network.Security;
using Microsoft.Extensions.Logging;

namespace Larpx.PersonalTools.TypeU.Services.Teacher;

/// <summary>
/// 教师端 UDP 发现服务：每 5 秒向子网广播教师端 IP+TCP 端口。
/// 启动监听即开始广播，停止监听即停止广播。
/// </summary>
public sealed class TeacherDiscoveryService
{
    private readonly UdpDiscoveryBroadcaster _broadcaster;
    private readonly ILogger<TeacherDiscoveryService>? _logger;
    private int _teacherPort;
    private string _teacherName;

    /// <summary>
    /// 初始化服务。
    /// </summary>
    /// <param name="broadcaster">UDP 广播器。</param>
    /// <param name="logger">日志。</param>
    /// <param name="teacherName">教师端标识名。</param>
    public TeacherDiscoveryService(
        UdpDiscoveryBroadcaster broadcaster,
        string teacherName = "TypeU-Teacher",
        ILogger<TeacherDiscoveryService>? logger = null)
    {
        _broadcaster = broadcaster ?? throw new ArgumentNullException(nameof(broadcaster));
        _teacherName = teacherName ?? throw new ArgumentNullException(nameof(teacherName));
        _logger = logger;
    }

    /// <summary>
    /// 启动广播。
    /// </summary>
    /// <param name="teacherPort">教师端 TCP 端口。</param>
    public void Start(int teacherPort)
    {
        _teacherPort = teacherPort;
        _broadcaster.Start(_teacherPort, _teacherName);
        _logger?.LogInformation("教师端发现服务已启动，端口 {Port}", teacherPort);
    }

    /// <summary>
    /// 停止广播。
    /// </summary>
    public void Stop()
    {
        _broadcaster.Stop();
        _logger?.LogInformation("教师端发现服务已停止");
    }
}
