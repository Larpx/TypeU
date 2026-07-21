using Larpx.PersonalTools.TypeU.Core.AntiCheat;
using Xunit;

namespace Larpx.PersonalTools.TypeU.Tests.Core;

/// <summary>
/// 批量上屏检测与速度监控测试。
/// </summary>
public class AntiCheatMonitorTests
{
    /// <summary>
    /// 单字符正常输入不应触发异常。
    /// </summary>
    [Fact]
    public void Verify_SingleChar_NoAnomaly()
    {
        var monitor = new AntiCheatMonitor();
        var result = monitor.Verify(1, timestampTicks: 1000);

        Assert.Equal(1, result.ValidCount);
        Assert.False(result.Anomaly);
    }

    /// <summary>
    /// 一次上屏 5 个字符应仅前 2 个有效，且标记异常。
    /// </summary>
    [Fact]
    public void Verify_FiveCharsAtOnce_OnlyTwoValidAndAnomaly()
    {
        var monitor = new AntiCheatMonitor();
        var result = monitor.Verify(5, timestampTicks: 1000);

        Assert.Equal(2, result.ValidCount);
        Assert.True(result.Anomaly);
        Assert.Contains("5 字符", result.Reason);
    }

    /// <summary>
    /// 两次单字符输入时间差小于 30ms 应触发异常。
    /// </summary>
    [Fact]
    public void Verify_TwoCharsWithin30ms_Anomaly()
    {
        var monitor = new AntiCheatMonitor();
        monitor.Verify(1, timestampTicks: 1000);
        var result = monitor.Verify(1, timestampTicks: 1020); // 差 20ms。

        Assert.True(result.Anomaly);
        Assert.Contains("时间差", result.Reason);
    }

    /// <summary>
    /// 两次单字符输入时间差大于 30ms 不应触发异常。
    /// </summary>
    [Fact]
    public void Verify_TwoCharsOver30ms_NoAnomaly()
    {
        var monitor = new AntiCheatMonitor();
        monitor.Verify(1, timestampTicks: 1000);
        var result = monitor.Verify(1, timestampTicks: 1040); // 差 40ms。

        Assert.False(result.Anomaly);
    }

    /// <summary>
    /// Reset 后历史时间差不应影响下一次检测。
    /// </summary>
    [Fact]
    public void Reset_ClearsHistory()
    {
        var monitor = new AntiCheatMonitor();
        monitor.Verify(1, timestampTicks: 1000);
        monitor.Reset();

        var result = monitor.Verify(1, timestampTicks: 1010);
        Assert.False(result.Anomaly);
    }

    /// <summary>
    /// 速度监控：10 秒窗口内输入 50 字符，应为 300 WPM（50 字 / 10s = 5 字/s = 300 WPM）。
    /// </summary>
    [Fact]
    public void SpeedMonitor_TenSecondsFiftyChars_Returns300Wpm()
    {
        var monitor = new SpeedMonitor(window: System.TimeSpan.FromMilliseconds(10000), thresholdWpm: 500);
        var start = 1000L;
        for (var i = 0; i < 50; i++)
        {
            monitor.OnInput(start + i * 200); // 50 字符分布在 10 秒内。
        }

        var wpm = monitor.GetCurrentWpm(start + 9800);
        Assert.InRange(wpm, 290, 320);
    }

    /// <summary>
    /// 速度超阈值应标记异常。
    /// </summary>
    [Fact]
    public void SpeedMonitor_OverThreshold_IsAnomaly()
    {
        var monitor = new SpeedMonitor(window: System.TimeSpan.FromMilliseconds(1000), thresholdWpm: 100);
        var now = 1000L;
        // 1 秒内输入 10 字符 = 600 WPM，远超 100。
        for (var i = 0; i < 10; i++)
        {
            monitor.OnInput(now + i * 100);
        }

        Assert.True(monitor.IsAnomaly(now + 1000));
    }

    /// <summary>
    /// 滑动窗口外的旧输入应被清除。
    /// </summary>
    [Fact]
    public void SpeedMonitor_OldInputsPurged()
    {
        var monitor = new SpeedMonitor(window: System.TimeSpan.FromMilliseconds(1000), thresholdWpm: 1000);
        var start = 1000L;
        for (var i = 0; i < 100; i++)
        {
            monitor.OnInput(start + i);
        }

        // 推进到 5 秒后，所有旧输入应被清除。
        var wpm = monitor.GetCurrentWpm(start + 5000);
        Assert.Equal(0, wpm);
    }
}
