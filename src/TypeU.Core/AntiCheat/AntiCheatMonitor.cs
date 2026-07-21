using System;
using System.Collections.Generic;

namespace Larpx.PersonalTools.TypeU.Core.AntiCheat;

/// <summary>
/// 防作弊监控器：基于输入事件的时间差与字符数检测批量上屏。
/// 规则：
/// 1. 单次输入事件中字符数超过 MaxValidCharsPerEvent 视为批量上屏，仅前 MaxValidCharsPerEvent 个有效。
/// 2. 两次输入事件时间差小于 BatchIntervalThresholdMs 视为批量上屏（连续输入过快）。
/// </summary>
public sealed class AntiCheatMonitor
{
    private long _lastInputTicks;
    private bool _hasLast;

    /// <summary>
    /// 处理一次输入事件（可能含多个字符）。
    /// </summary>
    /// <param name="charCount">本次事件上屏的字符数。</param>
    /// <param name="timestampTicks">事件时间戳（Environment.TickCount64 或同等单调时钟）。</param>
    /// <returns>检测结果。</returns>
    public InputVerifyResult Verify(int charCount, long timestampTicks)
    {
        if (charCount <= 0)
        {
            return new InputVerifyResult { ValidCount = 0, Anomaly = false };
        }

        var anomaly = false;
        var reasons = new List<string>();

        // 规则 1：单次事件字符数超阈值。
        if (charCount > InputVerifyResult.MaxValidCharsPerEvent)
        {
            anomaly = true;
            reasons.Add($"单次上屏 {charCount} 字符");
        }

        // 规则 2：与上次事件时间差过小。
        if (_hasLast)
        {
            var diffMs = (timestampTicks - _lastInputTicks);
            if (diffMs < InputVerifyResult.BatchIntervalThresholdMs)
            {
                anomaly = true;
                reasons.Add($"时间差 {diffMs}ms < {InputVerifyResult.BatchIntervalThresholdMs}ms");
            }
        }

        _lastInputTicks = timestampTicks;
        _hasLast = true;

        var validCount = Math.Min(charCount, InputVerifyResult.MaxValidCharsPerEvent);
        return new InputVerifyResult
        {
            ValidCount = validCount,
            Anomaly = anomaly,
            Reason = anomaly ? string.Join("; ", reasons) : null
        };
    }

    /// <summary>
    /// 重置监控状态（用于考试开始时清空历史记录）。
    /// </summary>
    public void Reset()
    {
        _lastInputTicks = 0;
        _hasLast = false;
    }
}
