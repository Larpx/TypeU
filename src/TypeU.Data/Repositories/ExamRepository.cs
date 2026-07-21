using System;
using System.Collections.Generic;
using System.Linq;
using Dapper;
using Larpx.PersonalTools.TypeU.Models.Entities;
using Larpx.PersonalTools.TypeU.Models.Enums;

namespace Larpx.PersonalTools.TypeU.Data.Repositories;

/// <summary>
/// 考试仓储：维护 ExamSessions 与 ExamRecords 两张表。
/// </summary>
public sealed class ExamRepository : RepositoryBase
{
    /// <summary>
    /// 初始化仓储。
    /// </summary>
    public ExamRepository(SqliteConnectionFactory factory) : base(factory)
    {
    }

    /// <summary>
    /// 插入考试会话。
    /// </summary>
    public void InsertSession(ExamSession session)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session));
        }

        using var conn = OpenConnection();
        conn.Execute(
            """
            INSERT INTO ExamSessions (SessionId, Mode, QuestionId, StartedAt, EndedAt, Duration)
            VALUES (@SessionId, @Mode, @QuestionId, @StartedAt, @EndedAt, @Duration);
            """,
            SessionRow.FromEntity(session));
    }

    /// <summary>
    /// 更新考试会话（主要用于结束时间填充）。
    /// </summary>
    public void UpdateSession(ExamSession session)
    {
        using var conn = OpenConnection();
        conn.Execute(
            """
            UPDATE ExamSessions
            SET Mode = @Mode,
                QuestionId = @QuestionId,
                StartedAt = @StartedAt,
                EndedAt = @EndedAt,
                Duration = @Duration
            WHERE SessionId = @SessionId;
            """,
            SessionRow.FromEntity(session));
    }

    /// <summary>
    /// 按 ID 查询会话。
    /// </summary>
    public ExamSession? GetSessionById(Guid sessionId)
    {
        using var conn = OpenConnection();
        var row = conn.QueryFirstOrDefault<SessionRow>(
            "SELECT SessionId, Mode, QuestionId, StartedAt, EndedAt, Duration FROM ExamSessions WHERE SessionId = @Id;",
            new { Id = sessionId.ToString("D") });
        return row?.ToEntity();
    }

    /// <summary>
    /// 查询全部会话。
    /// </summary>
    public IReadOnlyList<ExamSession> GetAllSessions()
    {
        using var conn = OpenConnection();
        var rows = conn.Query<SessionRow>(
            "SELECT SessionId, Mode, QuestionId, StartedAt, EndedAt, Duration FROM ExamSessions ORDER BY StartedAt DESC;");
        return rows.Select(r => r.ToEntity()).ToList();
    }

    /// <summary>
    /// 插入考试记录（学生成绩）。
    /// </summary>
    public void InsertRecord(ExamRecord record)
    {
        if (record is null)
        {
            throw new ArgumentNullException(nameof(record));
        }

        using var conn = OpenConnection();
        conn.Execute(
            """
            INSERT INTO ExamRecords (RecordId, SessionId, StudentId, Speed, Accuracy, Anomalies, SubmittedAt)
            VALUES (@RecordId, @SessionId, @StudentId, @Speed, @Accuracy, @Anomalies, @SubmittedAt);
            """,
            RecordRow.FromEntity(record));
    }

    /// <summary>
    /// 按会话查询全部成绩。
    /// </summary>
    public IReadOnlyList<ExamRecord> GetRecordsBySession(Guid sessionId)
    {
        using var conn = OpenConnection();
        var rows = conn.Query<RecordRow>(
            "SELECT RecordId, SessionId, StudentId, Speed, Accuracy, Anomalies, SubmittedAt FROM ExamRecords WHERE SessionId = @Id ORDER BY Speed DESC;",
            new { Id = sessionId.ToString("D") });
        return rows.Select(r => r.ToEntity()).ToList();
    }

    /// <summary>
    /// 按学号查询全部历史成绩。
    /// </summary>
    public IReadOnlyList<ExamRecord> GetRecordsByStudent(string studentId)
    {
        using var conn = OpenConnection();
        var rows = conn.Query<RecordRow>(
            "SELECT RecordId, SessionId, StudentId, Speed, Accuracy, Anomalies, SubmittedAt FROM ExamRecords WHERE StudentId = @Id ORDER BY SubmittedAt DESC;",
            new { Id = studentId });
        return rows.Select(r => r.ToEntity()).ToList();
    }

    private sealed class SessionRow
    {
        public string SessionId { get; set; } = string.Empty;
        public int Mode { get; set; }
        public string QuestionId { get; set; } = string.Empty;
        public string StartedAt { get; set; } = string.Empty;
        public string EndedAt { get; set; } = string.Empty;
        public int Duration { get; set; }

        public static SessionRow FromEntity(ExamSession e) => new()
        {
            SessionId = e.SessionId.ToString("D"),
            Mode = (int)e.Mode,
            QuestionId = e.QuestionId.ToString("D"),
            StartedAt = e.StartedAt.ToString("O"),
            EndedAt = e.EndedAt.ToString("O"),
            Duration = e.Duration
        };

        public ExamSession ToEntity() => new()
        {
            SessionId = Guid.Parse(SessionId),
            Mode = (ExamMode)Mode,
            QuestionId = Guid.Parse(QuestionId),
            StartedAt = DateTime.Parse(StartedAt, null, System.Globalization.DateTimeStyles.RoundtripKind),
            EndedAt = DateTime.Parse(EndedAt, null, System.Globalization.DateTimeStyles.RoundtripKind),
            Duration = Duration
        };
    }

    private sealed class RecordRow
    {
        public string RecordId { get; set; } = string.Empty;
        public string SessionId { get; set; } = string.Empty;
        public string StudentId { get; set; } = string.Empty;
        public double Speed { get; set; }
        public double Accuracy { get; set; }
        public string Anomalies { get; set; } = string.Empty;
        public string SubmittedAt { get; set; } = string.Empty;

        public static RecordRow FromEntity(ExamRecord e) => new()
        {
            RecordId = e.RecordId.ToString("D"),
            SessionId = e.SessionId.ToString("D"),
            StudentId = e.StudentId,
            Speed = e.Speed,
            Accuracy = e.Accuracy,
            Anomalies = e.Anomalies,
            SubmittedAt = e.SubmittedAt.ToString("O")
        };

        public ExamRecord ToEntity() => new()
        {
            RecordId = Guid.Parse(RecordId),
            SessionId = Guid.Parse(SessionId),
            StudentId = StudentId,
            Speed = Speed,
            Accuracy = Accuracy,
            Anomalies = Anomalies,
            SubmittedAt = DateTime.Parse(SubmittedAt, null, System.Globalization.DateTimeStyles.RoundtripKind)
        };
    }
}
