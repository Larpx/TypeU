using Larpx.PersonalTools.TypeU.Core.AntiCheat;
using Larpx.PersonalTools.TypeU.Core.Devices;
using Microsoft.Extensions.DependencyInjection;

namespace Larpx.PersonalTools.TypeU.Core.Bootstrapping;

/// <summary>
/// Core 层服务注册扩展。
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 注册 Core 层基础服务：设备指纹、防作弊监控、输入保护策略。
    /// </summary>
    /// <param name="services">服务容器。</param>
    /// <returns>服务容器本身，便于链式调用。</returns>
    public static IServiceCollection AddCoreServices(this IServiceCollection services)
    {
        services.AddSingleton<DeviceFingerprintProvider>();
        services.AddSingleton<AntiCheatMonitor>();
        services.AddSingleton<InputProtectionPolicy>();
        return services;
    }
}
