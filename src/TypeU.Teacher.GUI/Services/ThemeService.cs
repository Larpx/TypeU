using System;
using Avalonia;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Larpx.PersonalTools.TypeU.Teacher.GUI.Services;

/// <summary>
/// 主题服务：明暗主题切换。
/// </summary>
public sealed partial class ThemeService : ObservableObject
{
    /// <summary>
    /// 当前主题（Light/Dark）。
    /// </summary>
    [ObservableProperty]
    private ThemeVariant _currentTheme = ThemeVariant.Light;

    /// <summary>
    /// 切换主题。
    /// </summary>
    public void Toggle()
    {
        CurrentTheme = CurrentTheme == ThemeVariant.Light ? ThemeVariant.Dark : ThemeVariant.Light;
        Application.Current!.RequestedThemeVariant = CurrentTheme;
    }

    /// <summary>
    /// 设置主题。
    /// </summary>
    public void Set(ThemeVariant theme)
    {
        CurrentTheme = theme;
        Application.Current!.RequestedThemeVariant = theme;
    }
}
