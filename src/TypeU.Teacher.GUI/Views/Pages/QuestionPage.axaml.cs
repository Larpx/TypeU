using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Larpx.PersonalTools.TypeU.Teacher.GUI.ViewModels.Pages;

namespace Larpx.PersonalTools.TypeU.Teacher.GUI.Views.Pages;

/// <summary>
/// 题库管理页。
/// </summary>
public partial class QuestionPage : UserControl
{
    /// <summary>
    /// 初始化题库管理页。
    /// </summary>
    public QuestionPage()
    {
        InitializeComponent();
    }

    private async void OnImportClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not QuestionPageViewModel vm)
        {
            return;
        }
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null)
        {
            return;
        }
        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择 TXT 题库文件",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("文本文件") { Patterns = new[] { "*.txt" } }
            }
        });
        if (files.Count > 0)
        {
            await vm.ImportFromTxtAsync(files[0]);
        }
    }
}
