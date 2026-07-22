using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.Sqlite;

namespace Larpx.PersonalTools.TypeU.Data;

/// <summary>
/// SQLite 建表与增量迁移。
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
    /// 执行建表与增量迁移（可重复执行）。
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
        ExecuteScript(conn, tx, SqlCreateSessionLogins);
        EnsureColumn(conn, tx, "Questions", "ExpectedContent", "ALTER TABLE Questions ADD COLUMN ExpectedContent TEXT NOT NULL DEFAULT '';");
        EnsureColumn(conn, tx, "ExamSessions", "MaxAttempts", "ALTER TABLE ExamSessions ADD COLUMN MaxAttempts INTEGER NOT NULL DEFAULT 1;");
        EnsureColumn(conn, tx, "ExamSessions", "AllowPracticeAfterSubmit", "ALTER TABLE ExamSessions ADD COLUMN AllowPracticeAfterSubmit INTEGER NOT NULL DEFAULT 0;");
        EnsureColumn(conn, tx, "ExamSessions", "TeacherId", "ALTER TABLE ExamSessions ADD COLUMN TeacherId TEXT NOT NULL DEFAULT '';");
        EnsureColumn(conn, tx, "ExamSessions", "TeacherName", "ALTER TABLE ExamSessions ADD COLUMN TeacherName TEXT NOT NULL DEFAULT '';");
        EnsureColumn(conn, tx, "ExamSessions", "Status", "ALTER TABLE ExamSessions ADD COLUMN Status INTEGER NOT NULL DEFAULT 1;");
        EnsureColumn(conn, tx, "ExamRecords", "AttemptIndex", "ALTER TABLE ExamRecords ADD COLUMN AttemptIndex INTEGER NOT NULL DEFAULT 1;");
        tx.Commit();
    }

    private static void ExecuteScript(IDbConnection conn, IDbTransaction tx, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private static void EnsureColumn(IDbConnection conn, IDbTransaction tx, string table, string column, string alterSql)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var check = conn.CreateCommand();
        check.Transaction = tx;
        check.CommandText = $"PRAGMA table_info({table});";
        using var reader = check.ExecuteReader();
        while (reader.Read())
        {
            names.Add(reader.GetString(1));
        }

        reader.Close();
        if (!names.Contains(column))
        {
            ExecuteScript(conn, tx, alterSql);
        }
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
            CreatedAt TEXT NOT NULL,
            ExpectedContent TEXT NOT NULL DEFAULT ''
        );
        """;

    private const string SqlCreateExamSessions = """
        CREATE TABLE IF NOT EXISTS ExamSessions (
            SessionId TEXT NOT NULL PRIMARY KEY,
            Mode INTEGER NOT NULL,
            QuestionId TEXT NOT NULL,
            StartedAt TEXT NOT NULL,
            EndedAt TEXT NOT NULL,
            Duration INTEGER NOT NULL,
            MaxAttempts INTEGER NOT NULL DEFAULT 1,
            AllowPracticeAfterSubmit INTEGER NOT NULL DEFAULT 0,
            TeacherId TEXT NOT NULL DEFAULT '',
            TeacherName TEXT NOT NULL DEFAULT '',
            Status INTEGER NOT NULL DEFAULT 1
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
            SubmittedAt TEXT NOT NULL,
            AttemptIndex INTEGER NOT NULL DEFAULT 1
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

    private const string SqlCreateSessionLogins = """
        CREATE TABLE IF NOT EXISTS SessionLogins (
            SessionId TEXT NOT NULL,
            DeviceFingerprint TEXT NOT NULL,
            StudentId TEXT NOT NULL,
            Name TEXT NOT NULL,
            LoggedInAt TEXT NOT NULL,
            LogoutAllowed INTEGER NOT NULL DEFAULT 0,
            PRIMARY KEY (SessionId, DeviceFingerprint)
        );
        CREATE INDEX IF NOT EXISTS IX_SessionLogins_StudentId ON SessionLogins(StudentId);
        """;
}
