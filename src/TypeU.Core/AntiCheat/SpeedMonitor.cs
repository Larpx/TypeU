using System;
using System.Collections.Generic;

namespace Larpx.PersonalTools.TypeU.Core.AntiCheat;

/// <summary>
/// 速度异常滑动窗口监控器：在指定窗口内计算 WPM，超阈值标记异常。
/// </summary>
public sealed class SpeedMonitor
{
    private readonly TimeSpan _window;
    private readonly double _thresholdWpm;
    private readonly Queue<long> _timestamps = new();

    /// <summary>
    /// 初始化速度监控器。
    /// </summary>
    /// <param name="window">滑动窗口时长（默认 10 秒）。</param>
    /// <param name="thresholdWpm">异常阈值（WPM，默认 200）。</param>
    public SpeedMonitor(TimeSpan? window = null, double thresholdWpm = 200)
    {
        _window = window ?? TimeSpan.FromSeconds(10);
        _thresholdWpm = thresholdWpm;
    }

    /// <summary>
    /// 记录一次有效输入（一个字符）。
    /// </summary>
    public void OnInput(long timestampTicks)
    {
        _timestamps.Enqueue(timestampTicks);
        PurgeOld(timestampTicks);
    }

    /// <summary>
    /// 获取当前滑动窗口内的 WPM（字/分钟）。
    /// </summary>
    public double GetCurrentWpm(long nowTicks)
    {
        PurgeOld(nowTicks);
        if (_timestamps.Count == 0)
        {
            return 0;
        }

        var oldest = _timestamps.Peek();
        var elapsedMs = nowTicks - oldest;
        if (elapsedMs <= 0)
        {
            return 0;
        }

        var minutes = elapsedMs / 60000.0;
        return _timestamps.Count / minutes;
    }

    /// <summary>
    /// 当前 WPM 是否超过阈值。
    /// </summary>
    public bool IsAnomaly(long nowTicks)
    {
        return GetCurrentWpm(nowTicks) > _thresholdWpm;
    }

    private void PurgeOld(long nowTicks)
    {
        var cutoff = nowTicks - (long)_window.TotalMilliseconds;
        while (_timestamps.Count > 0 && _timestamps.Peek() < cutoff)
        {
            _timestamps.Dequeue();
        }
    }

    /// <summary>
    /// 重置监控状态。
    /// </summary>
    public void Reset()
    {
        _timestamps.Clear();
    }
}
