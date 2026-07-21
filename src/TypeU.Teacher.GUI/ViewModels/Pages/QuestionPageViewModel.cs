using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Larpx.PersonalTools.TypeU.Models.Entities;
using Larpx.PersonalTools.TypeU.Models.Enums;
using Larpx.PersonalTools.TypeU.Services.Teacher;
using Microsoft.Extensions.Logging;

namespace Larpx.PersonalTools.TypeU.Teacher.GUI.ViewModels.Pages;

/// <summary>
/// 题库管理页 ViewModel：列表 / 编辑 / TXT 导入。
/// </summary>
public sealed partial class QuestionPageViewModel : ViewModelBase
{
    private readonly QuestionService? _service;
    private readonly ILogger<QuestionPageViewModel>? _logger;

    /// <summary>设计时构造。</summary>
    public QuestionPageViewModel() : this(null, null)
    {
    }

    /// <summary>初始化题库管理页。</summary>
    public QuestionPageViewModel(QuestionService? service, ILogger<QuestionPageViewModel>? logger = null)
    {
        _service = service;
        _logger = logger;
        if (_service is not null)
        {
            Refresh();
        }
    }

    /// <summary>试题列表。</summary>
    public ObservableCollection<Question> Questions { get; } = new();

    /// <summary>选中的试题（用于编辑/删除）。</summary>
    [ObservableProperty]
    private Question? _selectedQuestion;

    /// <summary>新建/编辑时的试题类型。</summary>
    [ObservableProperty]
    private QuestionType _editType = QuestionType.Chinese;

    /// <summary>新建/编辑时的试题内容。</summary>
    [ObservableProperty]
    private string _editContent = string.Empty;

    /// <summary>状态提示文本。</summary>
    [ObservableProperty]
    private string _statusText = string.Empty;

    /// <summary>刷新列表。</summary>
    [RelayCommand]
    public void Refresh()
    {
        if (_service is null)
        {
            return;
        }
        Questions.Clear();
        foreach (var q in _service.GetAll())
        {
            Questions.Add(q);
        }
    }

    /// <summary>新增试题。</summary>
    [RelayCommand]
    private void Add()
    {
        if (_service is null)
        {
            StatusText = "服务未初始化";
            return;
        }
        if (string.IsNullOrWhiteSpace(EditContent))
        {
            StatusText = "内容不能为空";
            return;
        }
        _service.Add(EditType, EditContent);
        EditContent = string.Empty;
        StatusText = "已新增";
        Refresh();
    }

    /// <summary>更新选中试题。</summary>
    [RelayCommand]
    private void Update()
    {
        if (_service is null || SelectedQuestion is null)
        {
            return;
        }
        _service.Update(SelectedQuestion.QuestionId, EditType, EditContent);
        StatusText = "已更新";
        Refresh();
    }

    /// <summary>删除选中试题。</summary>
    [RelayCommand]
    private void Delete()
    {
        if (_service is null || SelectedQuestion is null)
        {
            return;
        }
        _service.Delete(SelectedQuestion.QuestionId);
        SelectedQuestion = null;
        StatusText = "已删除";
        Refresh();
    }

    /// <summary>
    /// 从 TXT 文件批量导入。
    /// </summary>
    /// <param name="file">存储文件引用。</param>
    public async Task ImportFromTxtAsync(IStorageFile? file)
    {
        if (_service is null || file is null)
        {
            return;
        }
        await using var stream = await file.OpenReadAsync();
        using var reader = new StreamReader(stream);
        var tempPath = Path.Combine(Path.GetTempPath(), $"typeu-import-{Guid.NewGuid():N}.txt");
        using (var fs = File.Create(tempPath))
        {
            await stream.CopyToAsync(fs);
        }
        try
        {
            var result = _service.ImportFromTxt(tempPath);
            StatusText = $"导入完成：成功 {result.Imported}，跳过 {result.Skipped}";
            _logger?.LogInformation("TXT 导入：成功 {Imported}，跳过 {Skipped}", result.Imported, result.Skipped);
            Refresh();
        }
        finally
        {
            try { File.Delete(tempPath); }
            catch { /* 忽略。 */ }
        }
    }

    /// <summary>选中试题变更时同步编辑表单。</summary>
    partial void OnSelectedQuestionChanged(Question? value)
    {
        if (value is null)
        {
            return;
        }
        EditType = value.Type;
        EditContent = value.Content;
    }
}
