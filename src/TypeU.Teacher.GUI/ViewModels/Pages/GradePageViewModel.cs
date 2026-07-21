using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Larpx.PersonalTools.TypeU.Data.Repositories;
using Larpx.PersonalTools.TypeU.Services.Teacher;
using Microsoft.Extensions.Logging;

namespace Larpx.PersonalTools.TypeU.Teacher.GUI.ViewModels.Pages;

/// <summary>
/// 成绩统计页 ViewModel：按会话查看成绩 + Excel 导出。
/// </summary>
public sealed partial class GradePageViewModel : ViewModelBase
{
    private readonly GradeService? _gradeService;
    private readonly ExamRepository? _examRepository;
    private readonly ILogger<GradePageViewModel>? _logger;

    /// <summary>设计时构造。</summary>
    public GradePageViewModel() : this(null, null, null)
    {
    }

    /// <summary>初始化成绩统计页。</summary>
    public GradePageViewModel(
        GradeService? gradeService,
        ExamRepository? examRepository,
        ILogger<GradePageViewModel>? logger = null)
    {
        _gradeService = gradeService;
        _examRepository = examRepository;
        _logger = logger;
    }

    /// <summary>成绩行列表（按学号/姓名/速度/正确率/异常/提交时间）。</summary>
    public ObservableCollection<GradeRow> Rows { get; } = new();

    /// <summary>状态文本。</summary>
    [ObservableProperty]
    private string _statusText = string.Empty;

    /// <summary>当前会话 ID 文本（用户输入）。</summary>
    [ObservableProperty]
    private string _sessionIdText = string.Empty;

    /// <summary>加载指定会话的成绩。</summary>
    [RelayCommand]
    public void Load()
    {
        if (_gradeService is null || !Guid.TryParse(SessionIdText, out var sessionId))
        {
            StatusText = "服务未初始化或会话 ID 非法";
            return;
        }
        var records = _gradeService.GetGradesBySession(sessionId);
        Rows.Clear();
        foreach (var r in records)
        {
            Rows.Add(new GradeRow
            {
                StudentId = r.StudentId,
                Name = r.Name,
                Speed = r.Speed,
                Accuracy = r.Accuracy,
                Anomalies = r.Anomalies,
                SubmittedAt = r.SubmittedAt
            });
        }
        StatusText = $"已加载 {Rows.Count} 条";
    }

    /// <summary>
    /// 导出当前会话成绩到 Excel。
    /// </summary>
    /// <param name="file">用户选择的保存目标。</param>
    public async Task ExportAsync(IStorageFile? file)
    {
        if (_gradeService is null || file is null || !Guid.TryParse(SessionIdText, out var sessionId))
        {
            return;
        }
        var tempPath = Path.Combine(Path.GetTempPath(), $"grades-{Guid.NewGuid():N}.xlsx");
        try
        {
            _gradeService.ExportToExcel(sessionId, tempPath);
            await using var stream = await file.OpenWriteAsync();
            using var fs = File.OpenRead(tempPath);
            await fs.CopyToAsync(stream);
            StatusText = "已导出";
            _logger?.LogInformation("Excel 已导出：会话 {SessionId}", sessionId);
        }
        finally
        {
            try { File.Delete(tempPath); }
            catch { /* 忽略。 */ }
        }
    }
}

/// <summary>
/// 成绩行（用于 UI 绑定）。
/// </summary>
public sealed class GradeRow
{
    /// <summary>学号。</summary>
    public string StudentId { get; set; } = string.Empty;

    /// <summary>姓名。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>速度（字/分钟）。</summary>
    public double Speed { get; set; }

    /// <summary>正确率（%）。</summary>
    public double Accuracy { get; set; }

    /// <summary>异常记录（JSON）。</summary>
    public string Anomalies { get; set; } = string.Empty;

    /// <summary>提交时间。</summary>
    public DateTime SubmittedAt { get; set; }
}
