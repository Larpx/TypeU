using System;
using System.IO;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;

namespace Larpx.PersonalTools.TypeU.Core.Devices;

/// <summary>
/// 设备指纹生成器：基于 CPU 参数 + 网卡 MAC + 机器唯一标识组合的 SHA-256 哈希。
/// Windows：读取注册表 MachineGuid；Linux：读取 /etc/machine-id；其他平台退化为 MachineName。
/// 替代硬盘序列号方案以保证跨平台兼容性。
/// </summary>
public sealed class DeviceFingerprintProvider
{
    /// <summary>
    /// 生成 64 字符的设备指纹（SHA-256 十六进制）。
    /// </summary>
    public string GetFingerprint()
    {
        var cpuId = GetCpuId();
        var mac = GetMacAddress();
        var machineId = GetMachineId();
        var combined = $"{cpuId}|{mac}|{machineId}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(combined));
        return Convert.ToHexString(hash);
    }

    private static string GetCpuId()
    {
        return $"{Environment.ProcessorCount}@{Environment.MachineName}";
    }

    private static string GetMacAddress()
    {
        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up)
                {
                    continue;
                }
                if (nic.NetworkInterfaceType is NetworkInterfaceType.Loopback)
                {
                    continue;
                }
                var addr = nic.GetPhysicalAddress();
                if (addr != null && addr.ToString().Length > 0)
                {
                    return addr.ToString();
                }
            }
        }
        catch
        {
            // 忽略：返回空字符串作为降级。
        }
        return "no-mac";
    }

    private static string GetMachineId()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            try
            {
                return File.ReadAllText("/etc/machine-id").Trim();
            }
            catch
            {
                // 降级。
            }
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
                if (key?.GetValue("MachineGuid") is string guid)
                {
                    return guid;
                }
            }
            catch
            {
                // 降级。
            }
        }

        return Environment.MachineName;
    }
}
