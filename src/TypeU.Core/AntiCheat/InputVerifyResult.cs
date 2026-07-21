using System;

namespace Larpx.PersonalTools.TypeU.Core.AntiCheat;

/// <summary>
/// 输入事件防作弊检测结果。
/// </summary>
public sealed class InputVerifyResult
{
    /// <summary>
    /// 真正计入成绩的有效字符数。
    /// </summary>
    public int ValidCount { get; init; }

    /// <summary>
    /// 是否检测到异常（批量上屏）。
    /// </summary>
    public bool Anomaly { get; init; }

    /// <summary>
    /// 异常原因（用于上报）。
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>
    /// 单次输入事件允许的最大有效字符数（超过部分视为批量上屏）。
    /// </summary>
    public const int MaxValidCharsPerEvent = 2;

    /// <summary>
    /// 时间差阈值（毫秒），小于该值视为批量上屏。
    /// </summary>
    public const int BatchIntervalThresholdMs = 30;
}
