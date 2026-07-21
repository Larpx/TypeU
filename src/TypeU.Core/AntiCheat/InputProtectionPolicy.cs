namespace Larpx.PersonalTools.TypeU.Core.AntiCheat;

/// <summary>
/// 输入保护策略：决定 UI 层是否应拦截剪贴板、右键菜单、拖拽等敏感操作。
/// 实际拦截由 UI 层（Avalonia 事件处理）完成，本类仅提供策略判断。
/// </summary>
public sealed class InputProtectionPolicy
{
    /// <summary>
    /// 是否处于考试锁定模式（启用时所有敏感操作应被拦截）。
    /// </summary>
    public bool IsExamLocked { get; private set; }

    /// <summary>
    /// 进入考试锁定模式。
    /// </summary>
    public void LockForExam()
    {
        IsExamLocked = true;
    }

    /// <summary>
    /// 退出考试锁定模式。
    /// </summary>
    public void Unlock()
    {
        IsExamLocked = false;
    }

    /// <summary>
    /// 是否应拦截剪贴板粘贴（Ctrl+V / 中键粘贴）。
    /// </summary>
    public bool ShouldBlockClipboardPaste() => IsExamLocked;

    /// <summary>
    /// 是否应拦截右键菜单。
    /// </summary>
    public bool ShouldBlockContextMenu() => IsExamLocked;

    /// <summary>
    /// 是否应拦截文本拖拽。
    /// </summary>
    public bool ShouldBlockDragDrop() => IsExamLocked;
}
