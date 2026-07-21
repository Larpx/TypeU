using System;
using Avalonia;

namespace Larpx.PersonalTools.TypeU.Student.GUI;

/// <summary>
/// 学生端应用程序入口。
/// </summary>
internal static class Program
{
    /// <summary>
    /// 应用程序主入口。
    /// </summary>
    /// <param name="args">启动参数。</param>
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    /// <summary>
    /// 构造 Avalonia 应用，供运行时与设计器共用。
    /// </summary>
    /// <returns>应用构造器。</returns>
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
