using System;
using System.Collections.Generic;
using System.Linq;
using Dapper;
using Larpx.PersonalTools.TypeU.Models.Entities;

namespace Larpx.PersonalTools.TypeU.Data.Repositories;

/// <summary>
/// Nonce 仓储：可选的持久化防重放存储（默认内存缓存即可，持久化用于跨进程防重放）。
/// </summary>
public sealed class NonceRepository : RepositoryBase
{
    /// <summary>
    /// 初始化仓储。
    /// </summary>
    public NonceRepository(SqliteConnectionFactory factory) : base(factory)
    {
    }

    /// <summary>
    /// 插入 Nonce（已存在则忽略）。
    /// </summary>
    /// <returns>true=新增成功；false=已存在。</returns>
    public bool TryAdd(string nonce, DateTime receivedAt)
    {
        using var conn = OpenConnection();
        try
        {
            conn.Execute(
                "INSERT INTO NonceCache (Nonce, ReceivedAt) VALUES (@Nonce, @ReceivedAt);",
                new { Nonce = nonce, ReceivedAt = receivedAt.ToString("O") });
            return true;
        }
        catch (Microsoft.Data.Sqlite.SqliteException)
        {
            return false;
        }
    }

    /// <summary>
    /// 删除早于指定时间的 Nonce（清理过期项）。
    /// </summary>
    public int PurgeOlderThan(DateTime cutoffUtc)
    {
        using var conn = OpenConnection();
        return conn.Execute(
            "DELETE FROM NonceCache WHERE ReceivedAt < @Cutoff;",
            new { Cutoff = cutoffUtc.ToString("O") });
    }

    /// <summary>
    /// 查询全部 Nonce（仅供诊断）。
    /// </summary>
    public IReadOnlyList<NonceCache> GetAll()
    {
        using var conn = OpenConnection();
        var rows = conn.Query<NonceRow>("SELECT Nonce, ReceivedAt FROM NonceCache;");
        return rows.Select(r => r.ToEntity()).ToList();
    }

    private sealed class NonceRow
    {
        public string Nonce { get; set; } = string.Empty;
        public string ReceivedAt { get; set; } = string.Empty;

        public NonceCache ToEntity() => new()
        {
            Nonce = Nonce,
            ReceivedAt = DateTime.Parse(ReceivedAt, null, System.Globalization.DateTimeStyles.RoundtripKind)
        };
    }
}
