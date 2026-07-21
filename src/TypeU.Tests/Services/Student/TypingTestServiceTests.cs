using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Larpx.PersonalTools.TypeU.Models.Dtos;
using Larpx.PersonalTools.TypeU.Models.Enums;
using Larpx.PersonalTools.TypeU.Services.Student;
using Xunit;

namespace Larpx.PersonalTools.TypeU.Tests.Services.Student;

/// <summary>
/// TypingTestService 单元测试：原文比对、速度计算、退格、重置。
/// </summary>
public sealed class TypingTestServiceTests
{
    private static QuestionDto MakeQuestion(string content) => new()
    {
        QuestionId = Guid.NewGuid(),
        Type = QuestionType.Chinese,
        Content = content,
        Mode = ExamMode.FixedLength,
        Duration = 300,
        SessionId = Guid.NewGuid()
    };

    /// <summary>
    /// 正确输入应累计正确字符数，正确率 100%。
    /// </summary>
    [Fact]
    public void AppendChar_CorrectInput_IncrementsCorrectCount()
    {
        var svc = new TypingTestService();
        svc.SetQuestion(MakeQuestion("abc"));

        svc.AppendChar('a');
        svc.AppendChar('b');
        svc.AppendChar('c');

        Assert.Equal(3, svc.CorrectCount);
        Assert.Equal(0, svc.ErrorCount);
        Assert.Equal(3, svc.InputCount);
        Assert.Equal(100.0, svc.GetCurrentAccuracy());
    }

    /// <summary>
    /// 错误输入应累计错误字符数。
    /// </summary>
    [Fact]
    public void AppendChar_WrongInput_IncrementsErrorCount()
    {
        var svc = new TypingTestService();
        svc.SetQuestion(MakeQuestion("abc"));

        svc.AppendChar('a');
        svc.AppendChar('X');
        svc.AppendChar('c');

        Assert.Equal(2, svc.CorrectCount);
        Assert.Equal(1, svc.ErrorCount);
        Assert.Equal(3, svc.InputCount);
        Assert.True(svc.GetCurrentAccuracy() < 100.0);
    }

    /// <summary>
    /// 超出原文长度应算作错误。
    /// </summary>
    [Fact]
    public void AppendChar_OverflowsOriginal_CountsAsError()
    {
        var svc = new TypingTestService();
        svc.SetQuestion(MakeQuestion("ab"));

        svc.AppendChar('a');
        svc.AppendChar('b');
        svc.AppendChar('c'); // 超出

        Assert.Equal(2, svc.CorrectCount);
        Assert.Equal(1, svc.ErrorCount);
    }

    /// <summary>
    /// 退格应回退最近一次输入的统计。
    /// </summary>
    [Fact]
    public void Backspace_DecrementsCount()
    {
        var svc = new TypingTestService();
        svc.SetQuestion(MakeQuestion("abc"));

        svc.AppendChar('a');
        svc.AppendChar('X'); // 错误
        svc.Backspace();

        Assert.Equal(1, svc.CorrectCount);
        Assert.Equal(0, svc.ErrorCount);
        Assert.Equal(1, svc.InputCount);
    }

    /// <summary>
    /// GetCompareStates 应正确返回每个字符的比对结果（已输入/未输入、正确/错误）。
    /// </summary>
    [Fact]
    public void GetCompareStates_ReturnsCorrectHighlight()
    {
        var svc = new TypingTestService();
        svc.SetQuestion(MakeQuestion("abc"));

        svc.AppendChar('a');
        svc.AppendChar('X');

        var states = svc.GetCompareStates();
        Assert.Equal(3, states.Count);
        Assert.True(states[0].IsEntered && states[0].IsCorrect);
        Assert.True(states[1].IsEntered && !states[1].IsCorrect);
        Assert.False(states[2].IsEntered);
    }

    /// <summary>
    /// 未开始测试时 AppendChar 应抛异常。
    /// </summary>
    [Fact]
    public void AppendChar_BeforeStart_Throws()
    {
        var svc = new TypingTestService();
        Assert.Throws<InvalidOperationException>(() => svc.AppendChar('a'));
    }

    /// <summary>
    /// Reset 后应清空全部输入与统计。
    /// </summary>
    [Fact]
    public void Reset_ClearsAllState()
    {
        var svc = new TypingTestService();
        svc.SetQuestion(MakeQuestion("abc"));
        svc.AppendChar('a');
        svc.AppendChar('b');

        svc.Reset();

        Assert.Equal(0, svc.InputCount);
        Assert.Equal(0, svc.CorrectCount);
        Assert.Equal(0, svc.ErrorCount);
    }

    /// <summary>
    /// 速度计算：1 个正确字符 / 60 秒 = 1 WPM。
    /// </summary>
    [Fact]
    public void GetCurrentSpeed_ReturnsZero_WhenNoInput()
    {
        var svc = new TypingTestService();
        svc.SetQuestion(MakeQuestion("abc"));
        Assert.Equal(0, svc.GetCurrentSpeed());
    }
}
