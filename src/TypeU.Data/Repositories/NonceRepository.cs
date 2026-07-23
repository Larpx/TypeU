using System;
using System.Collections.Generic;
using System.Linq;
using Larpx.PersonalTools.TypeU.Models.Entities;
using SqlSugar;

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
        using var db = CreateClient();
        // 使用 INSERT OR IGNORE 让 SQLite 自行处理主键冲突，通过受影响行数判断结果。
        // 切换 SqlSugar 后底层异常会被包装，不再依赖 catch SqliteException。
        var affected = db.Ado.ExecuteCommand(
            "INSERT OR IGNORE INTO NonceCache (Nonce, ReceivedAt) VALUES (@Nonce, @ReceivedAt);",
            new { Nonce = nonce, ReceivedAt = receivedAt.ToString("O") });
        return affected > 0;
    }

    /// <summary>
    /// 删除早于指定时间的 Nonce（清理过期项）。
    /// </summary>
    public int PurgeOlderThan(DateTime cutoffUtc)
    {
        using var db = CreateClient();
        return db.Ado.ExecuteCommand(
            "DELETE FROM NonceCache WHERE ReceivedAt < @Cutoff;",
            new { Cutoff = cutoffUtc.ToString("O") });
    }

    /// <summary>
    /// 查询全部 Nonce（仅供诊断）。
    /// </summary>
    public IReadOnlyList<NonceCache> GetAll()
    {
        using var db = CreateClient();
        var rows = db.Ado.SqlQuery<NonceRow>("SELECT Nonce, ReceivedAt FROM NonceCache;");
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
