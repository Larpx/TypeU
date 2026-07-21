using Avalonia.Controls;
using Avalonia.Interactivity;
using Larpx.PersonalTools.TypeU.Student.GUI.ViewModels.Pages;

namespace Larpx.PersonalTools.TypeU.Student.GUI.Views.Pages;

/// <summary>
/// 登录页视图。
/// </summary>
public partial class LoginPage : UserControl
{
    /// <summary>
    /// 初始化登录页。
    /// </summary>
    public LoginPage()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 页面加载完成时启动自动发现流程。
    /// </summary>
    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (DataContext is LoginPageViewModel vm)
        {
            _ = vm.StartDiscoveryCommand.ExecuteAsync(null);
        }
    }
}
