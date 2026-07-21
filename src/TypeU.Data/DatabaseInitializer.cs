using System;
using System.Data;
using Microsoft.Data.Sqlite;

namespace Larpx.PersonalTools.TypeU.Data;

/// <summary>
/// SQLite 建表脚本执行器：幂等创建 5 张表。
/// </summary>
public sealed class DatabaseInitializer
{
    private readonly SqliteConnectionFactory _factory;

    /// <summary>
    /// 初始化建表器。
    /// </summary>
    public DatabaseInitializer(SqliteConnectionFactory factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    /// <summary>
    /// 执行建表脚本（IF NOT EXISTS，可重复执行）。
    /// </summary>
    public void Initialize()
    {
        using var conn = _factory.Create();
        using var tx = conn.BeginTransaction();
        ExecuteScript(conn, tx, SqlCreateStudents);
        ExecuteScript(conn, tx, SqlCreateQuestions);
        ExecuteScript(conn, tx, SqlCreateExamSessions);
        ExecuteScript(conn, tx, SqlCreateExamRecords);
        ExecuteScript(conn, tx, SqlCreateNonceCache);
        tx.Commit();
    }

    private static void ExecuteScript(IDbConnection conn, IDbTransaction tx, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private const string SqlCreateStudents = """
        CREATE TABLE IF NOT EXISTS Students (
            StudentId TEXT NOT NULL PRIMARY KEY,
            Name TEXT NOT NULL,
            DeviceFingerprint TEXT NOT NULL,
            BoundAt TEXT NOT NULL,
            ExpiresAt TEXT NOT NULL,
            Status INTEGER NOT NULL DEFAULT 0
        );
        """;

    private const string SqlCreateQuestions = """
        CREATE TABLE IF NOT EXISTS Questions (
            QuestionId TEXT NOT NULL PRIMARY KEY,
            Type INTEGER NOT NULL,
            Content TEXT NOT NULL,
            CreatedAt TEXT NOT NULL
        );
        """;

    private const string SqlCreateExamSessions = """
        CREATE TABLE IF NOT EXISTS ExamSessions (
            SessionId TEXT NOT NULL PRIMARY KEY,
            Mode INTEGER NOT NULL,
            QuestionId TEXT NOT NULL,
            StartedAt TEXT NOT NULL,
            EndedAt TEXT NOT NULL,
            Duration INTEGER NOT NULL
        );
        """;

    private const string SqlCreateExamRecords = """
        CREATE TABLE IF NOT EXISTS ExamRecords (
            RecordId TEXT NOT NULL PRIMARY KEY,
            SessionId TEXT NOT NULL,
            StudentId TEXT NOT NULL,
            Speed REAL NOT NULL,
            Accuracy REAL NOT NULL,
            Anomalies TEXT NOT NULL,
            SubmittedAt TEXT NOT NULL
        );
        CREATE INDEX IF NOT EXISTS IX_ExamRecords_SessionId ON ExamRecords(SessionId);
        CREATE INDEX IF NOT EXISTS IX_ExamRecords_StudentId ON ExamRecords(StudentId);
        """;

    private const string SqlCreateNonceCache = """
        CREATE TABLE IF NOT EXISTS NonceCache (
            Nonce TEXT NOT NULL PRIMARY KEY,
            ReceivedAt TEXT NOT NULL
        );
        CREATE INDEX IF NOT EXISTS IX_NonceCache_ReceivedAt ON NonceCache(ReceivedAt);
        """;
}
