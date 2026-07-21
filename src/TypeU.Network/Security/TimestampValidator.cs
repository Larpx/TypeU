using System;
using Larpx.PersonalTools.TypeU.Network.Protocol;

namespace Larpx.PersonalTools.TypeU.Network.Security;

/// <summary>
/// 时间戳校验器：确保报文时间戳与本地时间偏差在容差内（默认 60 秒）。
/// </summary>
public sealed class TimestampValidator
{
    private readonly TimeSpan _tolerance;

    /// <summary>
    /// 初始化时间戳校验器，使用 spec 默认容差 60 秒。
    /// </summary>
    public TimestampValidator()
        : this(TimeSpan.FromSeconds(PacketConstants.TimestampToleranceSeconds))
    {
    }

    /// <summary>
    /// 初始化时间戳校验器。
    /// </summary>
    /// <param name="tolerance">允许的时间偏差。</param>
    public TimestampValidator(TimeSpan tolerance)
    {
        _tolerance = tolerance;
    }

    /// <summary>
    /// 校验报文时间戳是否在容差范围内。
    /// </summary>
    /// <param name="timestampMs">报文时间戳（UTC 毫秒）。</param>
    /// <param name="nowUtc">当前 UTC 时间。</param>
    /// <returns>true=合法；false=超时偏差。</returns>
    public bool IsValid(long timestampMs, DateTime nowUtc)
    {
        var nowMs = new DateTimeOffset(nowUtc).ToUnixTimeMilliseconds();
        var diff = Math.Abs(nowMs - timestampMs);
        return diff <= (long)_tolerance.TotalMilliseconds;
    }

    /// <summary>
    /// 获取当前 UTC 毫秒时间戳。
    /// </summary>
    public static long NowUtcMs() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}
