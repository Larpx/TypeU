using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading.Tasks;

namespace Larpx.PersonalTools.TypeU.Services.Teacher;

/// <summary>
/// 局域网扫描服务：一键扫描子网，识别已安装学生端的设备与未安装的设备。
/// 已安装学生端的设备会响应 LanScanResponse 报文；未响应的视为未安装。
/// </summary>
public sealed class LanDiscoveryService
{
    private readonly LanScanFeedback _feedback;

    /// <summary>
    /// 初始化服务。
    /// </summary>
    public LanDiscoveryService(LanScanFeedback? feedback = null)
    {
        _feedback = feedback ?? new LanScanFeedback();
    }

    /// <summary>
    /// 同步扫描指定子网（阻塞直到全部 Ping 完成或超时）。
    /// </summary>
    /// <param name="baseIp">子网基址，如 "192.168.1.0"。</param>
    /// <param name="timeoutMs">每个 IP 的 Ping 超时（毫秒）。</param>
    /// <returns>扫描结果列表。</returns>
    public IReadOnlyList<LanScanResult> Scan(string baseIp, int timeoutMs = 500)
    {
        var results = new List<LanScanResult>();
        var baseParts = baseIp.Split('.');
        if (baseParts.Length != 4)
        {
            throw new ArgumentException("baseIp 必须为 IPv4 点分格式。", nameof(baseIp));
        }

        var prefix = $"{baseParts[0]}.{baseParts[1]}.{baseParts[2]}.";

        for (var i = 1; i <= 254; i++)
        {
            var ipStr = prefix + i;
            var ip = IPAddress.Parse(ipStr);
            var reachable = PingHost(ip, timeoutMs);
            if (reachable)
            {
                results.Add(new LanScanResult
                {
                    Ip = ipStr,
                    Reachable = true,
                    StudentInstalled = _feedback.IsStudentInstalled(ipStr),
                    ComputerName = _feedback.GetComputerName(ipStr),
                    MacAddress = _feedback.GetMacAddress(ipStr)
                });
            }
            else
            {
                // Ping 不通也可能是学生端在线但 ICMP 被防火墙拦截；通过反馈表确认。
                if (_feedback.IsStudentInstalled(ipStr))
                {
                    results.Add(new LanScanResult
                    {
                        Ip = ipStr,
                        Reachable = false,
                        StudentInstalled = true,
                        ComputerName = _feedback.GetComputerName(ipStr),
                        MacAddress = _feedback.GetMacAddress(ipStr)
                    });
                }
            }
        }

        return results;
    }

    /// <summary>
    /// 手动添加未识别设备 IP（用于教师端补录未在扫描结果中出现的设备）。
    /// </summary>
    public LanScanResult AddManualIp(string ip)
    {
        return new LanScanResult
        {
            Ip = ip,
            Reachable = false,
            StudentInstalled = false,
            ComputerName = string.Empty,
            MacAddress = string.Empty,
            ManuallyAdded = true
        };
    }

    private static bool PingHost(IPAddress ip, int timeoutMs)
    {
        using var pinger = new Ping();
        try
        {
            var reply = pinger.Send(ip, timeoutMs);
            return reply?.Status == IPStatus.Success;
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// 扫描结果。
/// </summary>
public sealed class LanScanResult
{
    /// <summary>IP 地址。</summary>
    public string Ip { get; set; } = string.Empty;

    /// <summary>是否可 Ping 通。</summary>
    public bool Reachable { get; set; }

    /// <summary>是否已安装学生端。</summary>
    public bool StudentInstalled { get; set; }

    /// <summary>计算机名。</summary>
    public string ComputerName { get; set; } = string.Empty;

    /// <summary>MAC 地址。</summary>
    public string MacAddress { get; set; } = string.Empty;

    /// <summary>是否为手动添加（未通过扫描发现）。</summary>
    public bool ManuallyAdded { get; set; }
}

/// <summary>
/// 学生端反馈表：记录学生端主动上报的 IP/MAC/计算机名，供扫描结果分类。
/// 实际部署时由 TcpExamServer 在 ClientConnected 事件中填充。
/// </summary>
public sealed class LanScanFeedback
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, LanScanResult> _entries = new();

    /// <summary>
    /// 标记某 IP 已安装学生端。
    /// </summary>
    public void MarkStudentInstalled(string ip, string computerName, string macAddress)
    {
        _entries[ip] = new LanScanResult
        {
            Ip = ip,
            Reachable = true,
            StudentInstalled = true,
            ComputerName = computerName,
            MacAddress = macAddress
        };
    }

    /// <summary>
    /// 查询某 IP 是否已安装学生端（基于反馈表）。
    /// </summary>
    public bool IsStudentInstalled(string ip) => _entries.ContainsKey(ip);

    /// <summary>
    /// 查询计算机名。
    /// </summary>
    public string GetComputerName(string ip) =>
        _entries.TryGetValue(ip, out var r) ? r.ComputerName : string.Empty;

    /// <summary>
    /// 查询 MAC 地址。
    /// </summary>
    public string GetMacAddress(string ip) =>
        _entries.TryGetValue(ip, out var r) ? r.MacAddress : string.Empty;
}
