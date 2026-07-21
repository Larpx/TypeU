using System;
using Microsoft.Extensions.DependencyInjection;

namespace Larpx.PersonalTools.TypeU.Core.Bootstrapping;

/// <summary>
/// 应用程序 DI 启动器，统一装配服务容器。
/// </summary>
public static class AppBootstrapper
{
    /// <summary>
    /// 构造应用服务容器，依次注册 Core 与可选扩展服务。
    /// </summary>
    /// <param name="configure">附加服务注册回调（例如教师端/学生端特有服务与 ViewModel）。</param>
    /// <returns>已构造的服务提供者。</returns>
    public static IServiceProvider Build(Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddCoreServices();
        configure?.Invoke(services);
        return services.BuildServiceProvider();
    }
}
