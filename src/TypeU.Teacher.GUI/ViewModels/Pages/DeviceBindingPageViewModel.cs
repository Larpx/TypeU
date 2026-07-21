using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Larpx.PersonalTools.TypeU.Models.Entities;
using Larpx.PersonalTools.TypeU.Services.Teacher;
using Microsoft.Extensions.Logging;

namespace Larpx.PersonalTools.TypeU.Teacher.GUI.ViewModels.Pages;

/// <summary>
/// 设备绑定管理页 ViewModel：查看绑定时间/剩余时长 + 强制解绑。
/// </summary>
public sealed partial class DeviceBindingPageViewModel : ViewModelBase
{
    private readonly DeviceBindingService? _service;
    private readonly ILogger<DeviceBindingPageViewModel>? _logger;

    /// <summary>设计时构造。</summary>
    public DeviceBindingPageViewModel() : this(null, null)
    {
    }

    /// <summary>初始化设备绑定管理页。</summary>
    public DeviceBindingPageViewModel(DeviceBindingService? service, ILogger<DeviceBindingPageViewModel>? logger = null)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>绑定信息列表（按学号/姓名/指纹/绑定时间/剩余时长）。</summary>
    public ObservableCollection<BindingRow> Rows { get; } = new();

    /// <summary>状态文本。</summary>
    [ObservableProperty]
    private string _statusText = string.Empty;

    /// <summary>刷新绑定列表。</summary>
    [RelayCommand]
    public void Refresh()
    {
        if (_service is null)
        {
            return;
        }
        var bindings = _service.GetAllBindings();
        Rows.Clear();
        foreach (var s in bindings)
        {
            Rows.Add(new BindingRow
            {
                StudentId = s.StudentId,
                Name = s.Name,
                DeviceFingerprint = s.DeviceFingerprint,
                BoundAt = s.BoundAt,
                ExpiresAt = s.ExpiresAt,
                RemainingSeconds = _service.GetRemainingSeconds(s.StudentId)
            });
        }
        StatusText = $"已加载 {Rows.Count} 条";
    }

    /// <summary>强制解绑选中行。</summary>
    [RelayCommand]
    private void Unbind(BindingRow? row)
    {
        if (_service is null || row is null)
        {
            return;
        }
        _service.Unbind(row.StudentId);
        StatusText = $"已解绑 {row.StudentId}";
        _logger?.LogInformation("强制解绑 {StudentId}", row.StudentId);
        Refresh();
    }
}

/// <summary>
/// 绑定信息行（用于 UI 绑定）。
/// </summary>
public sealed class BindingRow
{
    /// <summary>学号。</summary>
    public string StudentId { get; set; } = string.Empty;

    /// <summary>姓名。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>设备指纹。</summary>
    public string DeviceFingerprint { get; set; } = string.Empty;

    /// <summary>绑定时间（UTC）。</summary>
    public DateTime BoundAt { get; set; }

    /// <summary>过期时间（UTC）。</summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>剩余秒数。</summary>
    public long RemainingSeconds { get; set; }

    /// <summary>剩余时长显示文本。</summary>
    public string RemainingDisplay
    {
        get
        {
            var ts = TimeSpan.FromSeconds(RemainingSeconds);
            if (ts <= TimeSpan.Zero)
            {
                return "已过期";
            }
            return $"{(int)ts.TotalHours}h {ts.Minutes}m";
        }
    }
}
