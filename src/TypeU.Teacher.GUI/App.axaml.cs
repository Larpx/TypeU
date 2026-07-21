using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Larpx.PersonalTools.TypeU.Core.Bootstrapping;
using Larpx.PersonalTools.TypeU.Data;
using Larpx.PersonalTools.TypeU.Data.Repositories;
using Larpx.PersonalTools.TypeU.Network.Discovery;
using Larpx.PersonalTools.TypeU.Network.Security;
using Larpx.PersonalTools.TypeU.Network.Tcp;
using Larpx.PersonalTools.TypeU.Services.Bootstrapping;
using Larpx.PersonalTools.TypeU.Services.Teacher;
using Larpx.PersonalTools.TypeU.Teacher.GUI.Services;
using Larpx.PersonalTools.TypeU.Teacher.GUI.ViewModels;
using Larpx.PersonalTools.TypeU.Teacher.GUI.ViewModels.Pages;
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

            // 网络与数据基础设施（演示用单例；实际部署需根据用户输入端口动态创建）。
            var aesKey = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
            var hmacKey = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
            const int UdpBroadcastPort = 5800;
            services.AddSingleton(_ => new PacketCodec(aesKey, hmacKey, verifyNonce: true));
            services.AddSingleton<TcpExamServer>();
            services.AddSingleton(sp => new UdpDiscoveryBroadcaster(
                sp.GetRequiredService<PacketCodec>(), UdpBroadcastPort));
            services.AddSingleton(_ => new SqliteConnectionFactory("Data Source=typeu-teacher.db"));
            services.AddSingleton<DatabaseInitializer>();

            // 仓储与服务。
            services.AddSingleton<QuestionRepository>();
            services.AddSingleton<ExamRepository>();
            services.AddSingleton<StudentRepository>();
            services.AddSingleton<QuestionService>();
            services.AddSingleton<DeviceBindingService>();
            services.AddSingleton<GradeService>();
            services.AddSingleton<MonitoringService>();
            services.AddSingleton<TeacherExamService>();
            services.AddSingleton<TimeSyncService>();
            services.AddSingleton<TeacherDiscoveryService>();
            services.AddSingleton<LanDiscoveryService>();

            // UI 服务与 ViewModel。
            services.AddSingleton<ThemeService>();
            services.AddSingleton<MainWindowViewModel>();
            services.AddSingleton<DashboardPageViewModel>();
            services.AddSingleton<QuestionPageViewModel>();
            services.AddSingleton<ExamControlPageViewModel>();
            services.AddSingleton<GradePageViewModel>();
            services.AddSingleton<DeviceBindingPageViewModel>();
            services.AddSingleton<LanScanPageViewModel>();
        });

        var logger = _services.GetRequiredService<ILogger<App>>();
        logger.LogInformation("教师端启动");

        // 初始化数据库表结构。
        try
        {
            _services.GetRequiredService<DatabaseInitializer>().Initialize();
            logger.LogInformation("数据库初始化完成：{File}", "typeu-teacher.db");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "数据库初始化失败");
        }

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
