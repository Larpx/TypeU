using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Larpx.PersonalTools.TypeU.Services.Teacher;
using Microsoft.Extensions.Logging;

namespace Larpx.PersonalTools.TypeU.Teacher.GUI.ViewModels.Pages;

/// <summary>
/// 局域网扫描页 ViewModel：一键扫描、设备列表展示、手动添加未识别 IP。
/// </summary>
public sealed partial class LanScanPageViewModel : ViewModelBase
{
    private readonly LanDiscoveryService? _service;
    private readonly ILogger<LanScanPageViewModel>? _logger;

    /// <summary>设计时构造。</summary>
    public LanScanPageViewModel() : this(null, null)
    {
    }

    /// <summary>初始化局域网扫描页。</summary>
    public LanScanPageViewModel(LanDiscoveryService? service, ILogger<LanScanPageViewModel>? logger = null)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>扫描结果列表。</summary>
    public ObservableCollection<LanScanResult> Devices { get; } = new();

    /// <summary>是否正在扫描。</summary>
    [ObservableProperty]
    private bool _isScanning;

    /// <summary>状态文本。</summary>
    [ObservableProperty]
    private string _statusText = "尚未扫描";

    /// <summary>手动添加 IP 输入。</summary>
    [ObservableProperty]
    private string _manualIp = string.Empty;

    /// <summary>当前子网基址（自动检测，可手动修改）。</summary>
    [ObservableProperty]
    private string _subnetBase = "192.168.1.0";

    /// <summary>一键扫描。</summary>
    [RelayCommand]
    private async Task ScanAsync()
    {
        if (_service is null)
        {
            StatusText = "服务未初始化";
            return;
        }
        IsScanning = true;
        StatusText = "扫描中...";
        try
        {
            AutoDetectSubnet();
            var results = await Task.Run(() => _service.Scan(SubnetBase));
            Devices.Clear();
            foreach (var d in results)
            {
                Devices.Add(d);
            }
            StatusText = $"扫描完成：{Devices.Count} 台";
        }
        catch (Exception ex)
        {
            StatusText = $"失败：{ex.Message}";
            _logger?.LogError(ex, "局域网扫描失败");
        }
        finally
        {
            IsScanning = false;
        }
    }

    /// <summary>手动添加 IP。</summary>
    [RelayCommand]
    private void AddManual()
    {
        if (_service is null || string.IsNullOrWhiteSpace(ManualIp))
        {
            return;
        }
        var entry = _service.AddManualIp(ManualIp);
        Devices.Add(entry);
        ManualIp = string.Empty;
        StatusText = "已添加";
    }

    /// <summary>自动检测本机所在子网基址。</summary>
    private void AutoDetectSubnet()
    {
        try
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Connect("8.8.8.8", 65530);
            var local = (IPEndPoint)socket.LocalEndPoint!;
            var bytes = local.Address.GetAddressBytes();
            bytes[3] = 0;
            SubnetBase = string.Join('.', bytes);
        }
        catch
        {
            // 保留默认值。
        }
    }
}
