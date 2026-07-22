using System;
using System.Collections.Generic;
using System.Text;
using Larpx.PersonalTools.TypeU.Models.Dtos;
using Larpx.PersonalTools.TypeU.Models.Enums;
using Microsoft.Extensions.Logging;

namespace Larpx.PersonalTools.TypeU.Services.Student;

/// <summary>
/// 输入字符比对结果（单个字符位置）。
/// </summary>
public sealed class CharCompareState
{
    /// <summary>字符位置。</summary>
    public int Index { get; init; }

    /// <summary>比对基准该位置的字符。</summary>
    public char Expected { get; init; }

    /// <summary>学生输入该位置的字符（未输入为 null）。</summary>
    public char? Actual { get; init; }

    /// <summary>是否正确。</summary>
    public bool IsCorrect { get; init; }

    /// <summary>是否已输入。</summary>
    public bool IsEntered { get; init; }
}

/// <summary>
/// 打字测试服务：原文比对、实时高亮、速度与正确率计算。
/// 定篇/限时：比对 Content；纠错模式：展示 Content（含错原文），比对 ExpectedContent。
/// 速度算法：有效字符数 / 已用分钟数（按字符计）。
/// 正确率：正确字符数 / 已输入字符数 * 100。
/// </summary>
public sealed class TypingTestService
{
    private readonly ILogger<TypingTestService>? _logger;
    private string _displayText = string.Empty;
    private string _compareText = string.Empty;
    private ExamMode _mode;
    private readonly StringBuilder _input = new();
    private int _correctCount;
    private int _errorCount;
    private DateTime _startUtc;
    private bool _started;

    /// <summary>
    /// 初始化服务。
    /// </summary>
    /// <param name="logger">可选日志。</param>
    public TypingTestService(ILogger<TypingTestService>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// 展示给学生的原文（纠错模式下为含错文本）。
    /// </summary>
    public string OriginalText => _displayText;

    /// <summary>
    /// 计分比对基准文本（纠错模式下为参考答案）。
    /// </summary>
    public string CompareText => _compareText;

    /// <summary>
    /// 当前考试模式。
    /// </summary>
    public ExamMode Mode => _mode;

    /// <summary>
    /// 是否为纠错模式。
    /// </summary>
    public bool IsErrorCorrection => _mode == ExamMode.ErrorCorrection;

    /// <summary>
    /// 已输入的全部文本。
    /// </summary>
    public string InputText => _input.ToString();

    /// <summary>
    /// 已输入字符数。
    /// </summary>
    public int InputCount => _input.Length;

    /// <summary>
    /// 正确字符数。
    /// </summary>
    public int CorrectCount => _correctCount;

    /// <summary>
    /// 错误字符数。
    /// </summary>
    public int ErrorCount => _errorCount;

    /// <summary>
    /// 比对基准总字符数。
    /// </summary>
    public int TotalChars => _compareText.Length;

    /// <summary>
    /// 是否已开始测试。
    /// </summary>
    public bool IsStarted => _started;

    /// <summary>
    /// 设置试题并重置输入状态（收到 QuestionPush 时调用）。
    /// </summary>
    /// <param name="question">试题 DTO。</param>
    public void SetQuestion(QuestionDto question)
    {
        if (question is null)
        {
            throw new ArgumentNullException(nameof(question));
        }

        _mode = question.Mode;
        _displayText = question.Content ?? string.Empty;
        if (_mode == ExamMode.ErrorCorrection)
        {
            if (string.IsNullOrWhiteSpace(question.ExpectedContent))
            {
                throw new InvalidOperationException("纠错模式试题缺少参考答案。");
            }

            _compareText = question.ExpectedContent;
        }
        else
        {
            _compareText = _displayText;
        }

        _input.Clear();
        _correctCount = 0;
        _errorCount = 0;
        _startUtc = DateTime.UtcNow;
        _started = true;
        _logger?.LogInformation("打字测试就绪：模式 {Mode}，展示 {Display} 字，比对 {Compare} 字",
            _mode, _displayText.Length, _compareText.Length);
    }

    /// <summary>
    /// 追加一个输入字符（已通过 AntiCheatMonitor 过滤）。
    /// </summary>
    /// <param name="c">输入字符。</param>
    /// <returns>该字符的比对结果。</returns>
    public CharCompareState AppendChar(char c)
    {
        if (!_started)
        {
            throw new InvalidOperationException("尚未开始测试。");
        }

        var index = _input.Length;
        _input.Append(c);

        var expected = index < _compareText.Length ? _compareText[index] : '\0';
        bool isCorrect;
        if (index >= _compareText.Length)
        {
            isCorrect = false;
            _errorCount++;
        }
        else if (expected == c)
        {
            isCorrect = true;
            _correctCount++;
        }
        else
        {
            isCorrect = false;
            _errorCount++;
        }

        return new CharCompareState
        {
            Index = index,
            Expected = expected,
            Actual = c,
            IsCorrect = isCorrect,
            IsEntered = true
        };
    }

    /// <summary>
    /// 退格删除最后一个字符。
    /// </summary>
    public void Backspace()
    {
        if (_input.Length == 0)
        {
            return;
        }

        var index = _input.Length - 1;
        var c = _input[index];
        _input.Remove(index, 1);

        if (index < _compareText.Length)
        {
            if (_compareText[index] == c)
            {
                _correctCount = Math.Max(0, _correctCount - 1);
            }
            else
            {
                _errorCount = Math.Max(0, _errorCount - 1);
            }
        }
        else
        {
            _errorCount = Math.Max(0, _errorCount - 1);
        }
    }

    /// <summary>
    /// 计算当前进度（已输入字符数）。
    /// </summary>
    /// <returns>已输入字符数。</returns>
    public int GetProgress() => _input.Length;

    /// <summary>
    /// 计算当前速度（字/分钟），按正确字符数 / 已用分钟数。
    /// </summary>
    /// <returns>字/分钟。</returns>
    public double GetCurrentSpeed()
    {
        if (!_started || _input.Length == 0)
        {
            return 0;
        }

        var elapsed = DateTime.UtcNow - _startUtc;
        if (elapsed.TotalMinutes <= 0)
        {
            return 0;
        }

        return _correctCount / elapsed.TotalMinutes;
    }

    /// <summary>
    /// 计算当前正确率（0-100）。
    /// </summary>
    /// <returns>正确率百分比。</returns>
    public double GetCurrentAccuracy()
    {
        if (_input.Length == 0)
        {
            return 100.0;
        }

        return _correctCount * 100.0 / _input.Length;
    }

    /// <summary>
    /// 获取比对基准全部字符的比对状态（用于输入高亮；非纠错模式下也可叠在原文上）。
    /// </summary>
    /// <returns>字符比对状态列表。</returns>
    public IReadOnlyList<CharCompareState> GetCompareStates()
    {
        var list = new List<CharCompareState>(_compareText.Length);
        for (var i = 0; i < _compareText.Length; i++)
        {
            if (i < _input.Length)
            {
                var actual = _input[i];
                list.Add(new CharCompareState
                {
                    Index = i,
                    Expected = _compareText[i],
                    Actual = actual,
                    IsCorrect = actual == _compareText[i],
                    IsEntered = true
                });
            }
            else
            {
                list.Add(new CharCompareState
                {
                    Index = i,
                    Expected = _compareText[i],
                    Actual = null,
                    IsCorrect = false,
                    IsEntered = false
                });
            }
        }

        return list;
    }

    /// <summary>
    /// 重置全部状态（重新考试时调用）。
    /// </summary>
    public void Reset()
    {
        _input.Clear();
        _correctCount = 0;
        _errorCount = 0;
        _startUtc = DateTime.UtcNow;
        _logger?.LogInformation("打字测试状态已重置");
    }
}
