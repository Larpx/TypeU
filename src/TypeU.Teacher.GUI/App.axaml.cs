using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Larpx.PersonalTools.TypeU.Teacher.GUI.ViewModels;
using Larpx.PersonalTools.TypeU.Teacher.GUI.Views;

namespace Larpx.PersonalTools.TypeU.Teacher.GUI;

/// <summary>
/// 教师端 Avalonia 应用程序。
/// </summary>
public partial class App : Application
{
    /// <summary>
    /// 初始化 XAML。
    /// </summary>
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    /// <summary>
    /// 框架初始化完成时创建主窗口。
    /// </summary>
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
