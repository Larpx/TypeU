using Larpx.PersonalTools.TypeU.Core.AntiCheat;
using Larpx.PersonalTools.TypeU.Core.Devices;
using Xunit;

namespace Larpx.PersonalTools.TypeU.Tests.Core;

/// <summary>
/// 设备指纹稳定性测试。
/// </summary>
public class DeviceFingerprintProviderTests
{
    /// <summary>
    /// 同一进程内多次生成的指纹应一致（确定式）。
    /// </summary>
    [Fact]
    public void GetFingerprint_SameProcess_Stable()
    {
        var provider = new DeviceFingerprintProvider();
        var first = provider.GetFingerprint();
        var second = provider.GetFingerprint();

        Assert.Equal(first, second);
    }

    /// <summary>
    /// 指纹应为 64 字符的十六进制字符串（SHA-256）。
    /// </summary>
    [Fact]
    public void GetFingerprint_ReturnsSha256Hex()
    {
        var provider = new DeviceFingerprintProvider();
        var fp = provider.GetFingerprint();

        Assert.Equal(64, fp.Length);
        foreach (var c in fp)
        {
            Assert.True(
                (c >= '0' && c <= '9') || (c >= 'A' && c <= 'F'),
                $"非法字符：{c}");
        }
    }
}
