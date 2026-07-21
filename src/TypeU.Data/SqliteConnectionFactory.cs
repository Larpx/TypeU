using System;
using System.Data;
using Microsoft.Data.Sqlite;

namespace Larpx.PersonalTools.TypeU.Data;

/// <summary>
/// SQLite 连接工厂：基于连接字符串创建 <see cref="SqliteConnection"/>。
/// </summary>
public sealed class SqliteConnectionFactory
{
    private readonly string _connectionString;

    /// <summary>
    /// 初始化连接工厂。
    /// </summary>
    /// <param name="connectionString">SQLite 连接字符串（如 "Data Source=typeu.db"）。</param>
    public SqliteConnectionFactory(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("连接字符串不能为空。", nameof(connectionString));
        }
        _connectionString = connectionString;
    }

    /// <summary>
    /// 创建一个新的 SQLite 连接（需由调用方 Dispose）。
    /// </summary>
    public IDbConnection Create()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        return conn;
    }
}
