using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Larpx.PersonalTools.TypeU.Core.Bootstrapping;
using Larpx.PersonalTools.TypeU.Core.Security;
using Larpx.PersonalTools.TypeU.Network.Discovery;
using Larpx.PersonalTools.TypeU.Network.Security;
using Larpx.PersonalTools.TypeU.Network.Tcp;
using Larpx.PersonalTools.TypeU.Services.Bootstrapping;
using Larpx.PersonalTools.TypeU.Services.Student;
using Larpx.PersonalTools.TypeU.Student.GUI.ViewModels;
using Larpx.PersonalTools.TypeU.Student.GUI.ViewModels.Pages;
using Larpx.PersonalTools.TypeU.Student.GUI.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Larpx.PersonalTools.TypeU.Student.GUI;

/// <summary>
/// 学生端 Avalonia 应用程序。
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
        const int UdpListenPort = 5800;

        _services = AppBootstrapper.Build(services =>
        {
            services.AddCommonServices("logs/student");

            // 网络基础设施：与教师端共享同一对预共享密钥。
            services.AddSingleton(_ => new PacketCodec(PreSharedKeys.AesKey, PreSharedKeys.HmacKey, verifyNonce: true));
            services.AddSingleton<TcpExamClient>();
            services.AddSingleton(sp => new UdpDiscoveryListener(
                sp.GetRequiredService<PacketCodec>(), UdpListenPort));

            // 学生端业务服务。
            services.AddSingleton<StudentAuthService>();
            services.AddSingleton<TypingTestService>();
            services.AddSingleton<StatusReportService>();
            services.AddSingleton<ResultSubmitService>();
            services.AddSingleton<ClientTimeSyncService>();
            services.AddSingleton<StudentDiscoveryService>();

            // ViewModel。
            services.AddSingleton<LoginPageViewModel>();
            services.AddSingleton<ExamPageViewModel>();
            services.AddSingleton<MainWindowViewModel>();
        });

        var logger = _services.GetRequiredService<ILogger<App>>();
        logger.LogInformation("学生端启动");

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
