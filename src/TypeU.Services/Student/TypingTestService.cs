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

    /// <summary>原文该位置的字符。</summary>
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
/// 速度算法：有效字符数 / 已用分钟数（WPM-like，按字符计）。
/// 正确率：正确字符数 / 已输入字符数 * 100。
/// </summary>
public sealed class TypingTestService
{
    private readonly ILogger<TypingTestService>? _logger;
    private string _originalText = string.Empty;
    private readonly StringBuilder _input = new();
    private int _correctCount;
    private int _errorCount;
    private DateTime _startUtc;
    private bool _started;

    /// <summary>
    /// 初始化服务。
    /// </summary>
    public TypingTestService(ILogger<TypingTestService>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// 当前试题原文（未开考时为空）。
    /// </summary>
    public string OriginalText => _originalText;

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
    /// 试题总字符数。
    /// </summary>
    public int TotalChars => _originalText.Length;

    /// <summary>
    /// 是否已开始测试。
    /// </summary>
    public bool IsStarted => _started;

    /// <summary>
    /// 设置试题并重置输入状态（收到 QuestionPush 时调用）。
    /// </summary>
    public void SetQuestion(QuestionDto question)
    {
        if (question is null)
        {
            throw new ArgumentNullException(nameof(question));
        }
        _originalText = question.Content ?? string.Empty;
        _input.Clear();
        _correctCount = 0;
        _errorCount = 0;
        _startUtc = DateTime.UtcNow;
        _started = true;
        _logger?.LogInformation("打字测试就绪：{Length} 字符", _originalText.Length);
    }

    /// <summary>
    /// 追加一个输入字符（已通过 AntiCheatMonitor 过滤）。
    /// 返回该字符的比对结果。
    /// </summary>
    public CharCompareState AppendChar(char c)
    {
        if (!_started)
        {
            throw new InvalidOperationException("尚未开始测试。");
        }

        var index = _input.Length;
        _input.Append(c);

        var expected = index < _originalText.Length ? _originalText[index] : '\0';
        bool isCorrect;
        if (index >= _originalText.Length)
        {
            // 超出原文长度：算作错误。
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

        if (index < _originalText.Length)
        {
            if (_originalText[index] == c)
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
    /// 计算当前进度（已输入字符数 / 总字符数）。
    /// </summary>
    public int GetProgress() => _input.Length;

    /// <summary>
    /// 计算当前速度（字/分钟），按有效字符数 / 已用分钟数。
    /// </summary>
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
    public double GetCurrentAccuracy()
    {
        if (_input.Length == 0)
        {
            return 100.0;
        }
        return _correctCount * 100.0 / _input.Length;
    }

    /// <summary>
    /// 获取全部字符的比对状态（用于 UI 高亮渲染）。
    /// </summary>
    public IReadOnlyList<CharCompareState> GetCompareStates()
    {
        var list = new List<CharCompareState>(_originalText.Length);
        for (var i = 0; i < _originalText.Length; i++)
        {
            if (i < _input.Length)
            {
                var actual = _input[i];
                list.Add(new CharCompareState
                {
                    Index = i,
                    Expected = _originalText[i],
                    Actual = actual,
                    IsCorrect = actual == _originalText[i],
                    IsEntered = true
                });
            }
            else
            {
                list.Add(new CharCompareState
                {
                    Index = i,
                    Expected = _originalText[i],
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
