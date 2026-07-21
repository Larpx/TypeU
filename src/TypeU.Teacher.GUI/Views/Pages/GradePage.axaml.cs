using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Larpx.PersonalTools.TypeU.Teacher.GUI.ViewModels.Pages;

namespace Larpx.PersonalTools.TypeU.Teacher.GUI.Views.Pages;

/// <summary>
/// 成绩统计页。
/// </summary>
public partial class GradePage : UserControl
{
    /// <summary>
    /// 初始化成绩统计页。
    /// </summary>
    public GradePage()
    {
        InitializeComponent();
    }

    private async void OnExportClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not GradePageViewModel vm)
        {
            return;
        }
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null)
        {
            return;
        }
        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "导出成绩到 Excel",
            DefaultExtension = "xlsx",
            SuggestedFileName = "grades"
        });
        await vm.ExportAsync(file);
    }
}
