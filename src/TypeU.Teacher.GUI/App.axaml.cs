using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Larpx.PersonalTools.TypeU.Core.Bootstrapping;
using Larpx.PersonalTools.TypeU.Core.Security;
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
    private const int TcpListenPort = 5700;
    private const int UdpBroadcastPort = 5800;
    private IServiceProvider? _services;
    private ILogger<App>? _logger;

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

            // 网络与数据基础设施。
            services.AddSingleton(_ => new PacketCodec(PreSharedKeys.AesKey, PreSharedKeys.HmacKey, verifyNonce: true));
            services.AddSingleton<TcpExamServer>();
            services.AddSingleton(sp => new UdpDiscoveryBroadcaster(
                sp.GetRequiredService<PacketCodec>(), UdpBroadcastPort));
            services.AddSingleton(_ => new SqliteConnectionFactory("Data Source=typeu-teacher.db"));
            services.AddSingleton<DatabaseInitializer>();

            // 仓储与服务。
            services.AddSingleton<QuestionRepository>();
            services.AddSingleton<ExamRepository>();
            services.AddSingleton<StudentRepository>();
            services.AddSingleton<SessionLoginRepository>();
            services.AddSingleton<QuestionService>();
            services.AddSingleton<DeviceBindingService>();
            services.AddSingleton<GradeService>();
            services.AddSingleton<MonitoringService>();
            services.AddSingleton<TeacherExamService>();
            services.AddSingleton<TimeSyncService>();
            services.AddSingleton<TeacherDiscoveryService>();
            services.AddSingleton<LanDiscoveryService>();
            services.AddSingleton<TeacherPacketHandler>();

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

        _logger = _services.GetRequiredService<ILogger<App>>();
        _logger.LogInformation("教师端启动");

        // 初始化数据库表结构。
        try
        {
            _services.GetRequiredService<DatabaseInitializer>().Initialize();
            _logger.LogInformation("数据库初始化完成：{File}", "typeu-teacher.db");
            _services.GetRequiredService<TeacherExamService>().TryRestoreRunningSession();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "数据库初始化失败");
        }

        // 启动 TCP 监听、UDP 广播、时间同步服务。
        try
        {
            _services.GetRequiredService<TcpExamServer>().Start(TcpListenPort);
            _logger.LogInformation("TCP 服务端已启动，端口 {Port}", TcpListenPort);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TCP 服务端启动失败");
        }
        try
        {
            _services.GetRequiredService<TeacherDiscoveryService>().Start(TcpListenPort);
            _logger.LogInformation("UDP 发现服务已启动");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UDP 发现服务启动失败");
        }
        try
        {
            _services.GetRequiredService<TimeSyncService>().Start();
            _logger.LogInformation("时间同步服务已启动");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "时间同步服务启动失败");
        }

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = _services.GetRequiredService<MainWindowViewModel>(),
            };
            desktop.Exit += OnAppExit;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnAppExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        _logger?.LogInformation("教师端正在关闭");
        try
        {
            _services?.GetRequiredService<TimeSyncService>().Stop();
        }
        catch { /* 忽略关闭异常。 */ }
        try
        {
            _services?.GetRequiredService<TeacherDiscoveryService>().Stop();
        }
        catch { /* 忽略关闭异常。 */ }
        try
        {
            _services?.GetRequiredService<TcpExamServer>().Stop();
        }
        catch { /* 忽略关闭异常。 */ }
        _logger?.LogInformation("教师端已关闭");
    }
}
