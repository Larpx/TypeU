using Larpx.PersonalTools.TypeU.Services.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace Larpx.PersonalTools.TypeU.Services.Bootstrapping;

/// <summary>
/// 业务服务层注册扩展（Task 7/8 将补充教师端/学生端业务服务）。
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 注册通用业务服务：日志等。
    /// </summary>
    /// <param name="services">服务容器。</param>
    /// <param name="logDirectory">日志输出目录。</param>
    /// <returns>服务容器本身，便于链式调用。</returns>
    public static IServiceCollection AddCommonServices(this IServiceCollection services, string logDirectory = "logs")
    {
        services.AddTypeULogging(logDirectory);
        return services;
    }
}
