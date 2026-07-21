using Larpx.PersonalTools.TypeU.Core.AntiCheat;
using Xunit;

namespace Larpx.PersonalTools.TypeU.Tests.Core;

/// <summary>
/// 输入保护策略测试。
/// </summary>
public class InputProtectionPolicyTests
{
    /// <summary>
    /// 默认（非考试）状态下应允许所有操作。
    /// </summary>
    [Fact]
    public void Default_AllowsAll()
    {
        var policy = new InputProtectionPolicy();
        Assert.False(policy.ShouldBlockClipboardPaste());
        Assert.False(policy.ShouldBlockContextMenu());
        Assert.False(policy.ShouldBlockDragDrop());
    }

    /// <summary>
    /// 进入考试锁定后应阻止所有敏感操作。
    /// </summary>
    [Fact]
    public void LockForExam_BlocksAll()
    {
        var policy = new InputProtectionPolicy();
        policy.LockForExam();

        Assert.True(policy.ShouldBlockClipboardPaste());
        Assert.True(policy.ShouldBlockContextMenu());
        Assert.True(policy.ShouldBlockDragDrop());
    }

    /// <summary>
    /// 退出锁定后应恢复允许状态。
    /// </summary>
    [Fact]
    public void Unlock_RestoresAllowed()
    {
        var policy = new InputProtectionPolicy();
        policy.LockForExam();
        policy.Unlock();

        Assert.False(policy.ShouldBlockClipboardPaste());
        Assert.False(policy.ShouldBlockContextMenu());
        Assert.False(policy.ShouldBlockDragDrop());
    }
}
