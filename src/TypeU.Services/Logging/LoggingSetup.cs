using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Extensions.Logging;

namespace Larpx.PersonalTools.TypeU.Services.Logging;

/// <summary>
/// Serilog 日志配置工具：控制台 + 按日滚动文件。
/// </summary>
public static class LoggingSetup
{
    /// <summary>
    /// 创建并配置 Serilog 日志记录器。
    /// </summary>
    /// <param name="logDirectory">日志输出目录，默认 logs。</param>
    /// <returns>Serilog 日志记录器。</returns>
    public static Serilog.ILogger CreateLogger(string logDirectory = "logs")
    {
        var fullPath = Path.GetFullPath(logDirectory);
        Directory.CreateDirectory(fullPath);

        var config = new LoggerConfiguration()
#if DEBUG
            .MinimumLevel.Debug()
#else
            .MinimumLevel.Information()
#endif
            .WriteTo.Console()
            .WriteTo.File(
                Path.Combine(fullPath, "typeu-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14);

        return config.CreateLogger();
    }

    /// <summary>
    /// 将 Serilog 注册为 Microsoft.Extensions.Logging 的日志提供者，并设置全局 Log.Logger。
    /// </summary>
    /// <param name="services">服务容器。</param>
    /// <param name="logDirectory">日志输出目录。</param>
    /// <returns>服务容器本身，便于链式调用。</returns>
    public static IServiceCollection AddTypeULogging(this IServiceCollection services, string logDirectory = "logs")
    {
        var serilogLogger = CreateLogger(logDirectory);
        Log.Logger = serilogLogger;
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddProvider(new SerilogLoggerProvider(serilogLogger));
        });
        return services;
    }
}
