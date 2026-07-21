using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Larpx.PersonalTools.TypeU.Network.Protocol;

namespace Larpx.PersonalTools.TypeU.Network.Security;

/// <summary>
/// Nonce 防重放缓存：基于滑动窗口记录最近 120 秒内见过的 Nonce。
/// </summary>
public sealed class NonceCache
{
    private readonly ConcurrentDictionary<string, long> _seen = new();
    private readonly TimeSpan _window;
    private readonly TimeSpan _cleanupInterval;
    private long _lastCleanupTicks;

    /// <summary>
    /// 初始化 Nonce 缓存，默认窗口 120 秒。
    /// </summary>
    public NonceCache()
        : this(TimeSpan.FromSeconds(PacketConstants.NonceCacheWindowSeconds))
    {
    }

    /// <summary>
    /// 初始化 Nonce 缓存。
    /// </summary>
    /// <param name="window">缓存窗口时长。</param>
    public NonceCache(TimeSpan window)
    {
        _window = window;
        _cleanupInterval = TimeSpan.FromMilliseconds(window.TotalMilliseconds / 2);
        _lastCleanupTicks = Environment.TickCount64;
    }

    /// <summary>
    /// 检查 Nonce 是否首次出现（true 表示首次，可接受；false 表示重复，应拒绝）。
    /// </summary>
    /// <param name="nonce">16 字节 Nonce。</param>
    /// <param name="nowTicks">当前时间戳（Environment.TickCount64）。</param>
    /// <returns>true=首次出现；false=已存在（重放）。</returns>
    public bool TryAdd(byte[] nonce, long nowTicks)
    {
        if (nonce is null || nonce.Length != PacketConstants.NonceLength)
        {
            return false;
        }

        MaybeCleanup(nowTicks);

        var key = Convert.ToHexString(nonce);
        var expiresAt = nowTicks + (long)_window.TotalMilliseconds;
        return _seen.TryAdd(key, expiresAt);
    }

    /// <summary>
    /// 清理过期项（惰性触发，避免每次都全表扫描）。
    /// </summary>
    private void MaybeCleanup(long nowTicks)
    {
        var last = Interlocked.Read(ref _lastCleanupTicks);
        if (nowTicks - last < (long)_cleanupInterval.TotalMilliseconds)
        {
            return;
        }

        Interlocked.Exchange(ref _lastCleanupTicks, nowTicks);

        foreach (var pair in _seen)
        {
            if (pair.Value <= nowTicks)
            {
                ((ICollection<KeyValuePair<string, long>>)_seen).Remove(pair);
            }
        }
    }

    /// <summary>
    /// 当前缓存条目数（仅供诊断）。
    /// </summary>
    public int Count => _seen.Count;
}
