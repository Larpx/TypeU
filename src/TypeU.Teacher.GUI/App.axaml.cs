using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Larpx.PersonalTools.TypeU.Core.Bootstrapping;
using Larpx.PersonalTools.TypeU.Services.Bootstrapping;
using Larpx.PersonalTools.TypeU.Teacher.GUI.ViewModels;
using Larpx.PersonalTools.TypeU.Teacher.GUI.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Larpx.PersonalTools.TypeU.Teacher.GUI;

/// <summary>
/// 教师端 Avalonia 应用程序。
/// </summary>
public partial class App : Application
{
    private IServiceProvider? _services;

    /// <summary>
    /// 初始化 XAML。
    /// </summary>
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    /// <summary>
    /// 框架初始化完成时装配 DI 容器、写入启动日志并创建主窗口。
    /// </summary>
    public override void OnFrameworkInitializationCompleted()
    {
        _services = AppBootstrapper.Build(services =>
        {
            services.AddCommonServices("logs/teacher");
            services.AddSingleton<MainWindowViewModel>();
        });

        var logger = _services.GetRequiredService<ILogger<App>>();
        logger.LogInformation("教师端启动");

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = _services.GetRequiredService<MainWindowViewModel>(),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
