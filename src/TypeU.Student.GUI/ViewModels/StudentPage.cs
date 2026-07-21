namespace Larpx.PersonalTools.TypeU.Student.GUI.ViewModels;

/// <summary>
/// 学生端页面枚举：登录页与考试页互斥切换。
/// </summary>
public enum StudentPage
{
    /// <summary>登录签到页（输入学号/姓名、自动发现、手动 IP）。</summary>
    Login = 0,

    /// <summary>沉浸式考试页（全屏置顶、原文/输入/仪表盘）。</summary>
    Exam = 1
}
