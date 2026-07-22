using System;
using System.Collections.Generic;
using System.Linq;
using Dapper;
using Larpx.PersonalTools.TypeU.Models.Entities;
using Larpx.PersonalTools.TypeU.Models.Enums;

namespace Larpx.PersonalTools.TypeU.Data.Repositories;

/// <summary>
/// 考试仓储：ExamSessions / ExamRecords。
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
            INSERT INTO ExamSessions (SessionId, Mode, QuestionId, StartedAt, EndedAt, Duration,
                MaxAttempts, AllowPracticeAfterSubmit, TeacherId, TeacherName, Status)
            VALUES (@SessionId, @Mode, @QuestionId, @StartedAt, @EndedAt, @Duration,
                @MaxAttempts, @AllowPracticeAfterSubmit, @TeacherId, @TeacherName, @Status);
            """,
            SessionRow.FromEntity(session));
    }

    /// <summary>
    /// 更新考试会话。
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
                Duration = @Duration,
                MaxAttempts = @MaxAttempts,
                AllowPracticeAfterSubmit = @AllowPracticeAfterSubmit,
                TeacherId = @TeacherId,
                TeacherName = @TeacherName,
                Status = @Status
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
            """
            SELECT SessionId, Mode, QuestionId, StartedAt, EndedAt, Duration,
                   MaxAttempts, AllowPracticeAfterSubmit, TeacherId, TeacherName, Status
            FROM ExamSessions WHERE SessionId = @Id;
            """,
            new { Id = sessionId.ToString("D") });
        return row?.ToEntity();
    }

    /// <summary>
    /// 查询当前进行中的会话（最多一条）。
    /// </summary>
    public ExamSession? GetRunningSession()
    {
        using var conn = OpenConnection();
        var row = conn.QueryFirstOrDefault<SessionRow>(
            """
            SELECT SessionId, Mode, QuestionId, StartedAt, EndedAt, Duration,
                   MaxAttempts, AllowPracticeAfterSubmit, TeacherId, TeacherName, Status
            FROM ExamSessions WHERE Status = @Status ORDER BY StartedAt DESC LIMIT 1;
            """,
            new { Status = (int)ExamSessionStatus.Running });
        return row?.ToEntity();
    }

    /// <summary>
    /// 查询全部会话。
    /// </summary>
    public IReadOnlyList<ExamSession> GetAllSessions()
    {
        using var conn = OpenConnection();
        var rows = conn.Query<SessionRow>(
            """
            SELECT SessionId, Mode, QuestionId, StartedAt, EndedAt, Duration,
                   MaxAttempts, AllowPracticeAfterSubmit, TeacherId, TeacherName, Status
            FROM ExamSessions ORDER BY StartedAt DESC;
            """);
        return rows.Select(r => r.ToEntity()).ToList();
    }

    /// <summary>
    /// 插入考试记录。
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
            INSERT INTO ExamRecords (RecordId, SessionId, StudentId, Speed, Accuracy, Anomalies, SubmittedAt, AttemptIndex)
            VALUES (@RecordId, @SessionId, @StudentId, @Speed, @Accuracy, @Anomalies, @SubmittedAt, @AttemptIndex);
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
            """
            SELECT RecordId, SessionId, StudentId, Speed, Accuracy, Anomalies, SubmittedAt, AttemptIndex
            FROM ExamRecords WHERE SessionId = @Id ORDER BY StudentId, AttemptIndex;
            """,
            new { Id = sessionId.ToString("D") });
        return rows.Select(r => r.ToEntity()).ToList();
    }

    /// <summary>
    /// 查询某生在某会话已提交次数。
    /// </summary>
    public int CountAttempts(Guid sessionId, string studentId)
    {
        using var conn = OpenConnection();
        return conn.ExecuteScalar<int>(
            "SELECT COUNT(1) FROM ExamRecords WHERE SessionId = @SessionId AND StudentId = @StudentId;",
            new { SessionId = sessionId.ToString("D"), StudentId = studentId });
    }

    /// <summary>
    /// 按学号查询全部历史成绩。
    /// </summary>
    public IReadOnlyList<ExamRecord> GetRecordsByStudent(string studentId)
    {
        using var conn = OpenConnection();
        var rows = conn.Query<RecordRow>(
            """
            SELECT RecordId, SessionId, StudentId, Speed, Accuracy, Anomalies, SubmittedAt, AttemptIndex
            FROM ExamRecords WHERE StudentId = @Id ORDER BY SubmittedAt DESC;
            """,
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
        public int MaxAttempts { get; set; } = 1;
        public int AllowPracticeAfterSubmit { get; set; }
        public string TeacherId { get; set; } = string.Empty;
        public string TeacherName { get; set; } = string.Empty;
        public int Status { get; set; } = 1;

        public static SessionRow FromEntity(ExamSession e) => new()
        {
            SessionId = e.SessionId.ToString("D"),
            Mode = (int)e.Mode,
            QuestionId = e.QuestionId.ToString("D"),
            StartedAt = e.StartedAt.ToString("O"),
            EndedAt = e.EndedAt.ToString("O"),
            Duration = e.Duration,
            MaxAttempts = e.MaxAttempts,
            AllowPracticeAfterSubmit = e.AllowPracticeAfterSubmit ? 1 : 0,
            TeacherId = e.TeacherId ?? string.Empty,
            TeacherName = e.TeacherName ?? string.Empty,
            Status = (int)e.Status
        };

        public ExamSession ToEntity() => new()
        {
            SessionId = Guid.Parse(SessionId),
            Mode = (ExamMode)Mode,
            QuestionId = Guid.Parse(QuestionId),
            StartedAt = DateTime.Parse(StartedAt, null, System.Globalization.DateTimeStyles.RoundtripKind),
            EndedAt = DateTime.Parse(EndedAt, null, System.Globalization.DateTimeStyles.RoundtripKind),
            Duration = Duration,
            MaxAttempts = MaxAttempts <= 0 ? 1 : MaxAttempts,
            AllowPracticeAfterSubmit = AllowPracticeAfterSubmit != 0,
            TeacherId = TeacherId ?? string.Empty,
            TeacherName = TeacherName ?? string.Empty,
            Status = (ExamSessionStatus)Status
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
        public int AttemptIndex { get; set; } = 1;

        public static RecordRow FromEntity(ExamRecord e) => new()
        {
            RecordId = e.RecordId.ToString("D"),
            SessionId = e.SessionId.ToString("D"),
            StudentId = e.StudentId,
            Speed = e.Speed,
            Accuracy = e.Accuracy,
            Anomalies = e.Anomalies,
            SubmittedAt = e.SubmittedAt.ToString("O"),
            AttemptIndex = e.AttemptIndex <= 0 ? 1 : e.AttemptIndex
        };

        public ExamRecord ToEntity() => new()
        {
            RecordId = Guid.Parse(RecordId),
            SessionId = Guid.Parse(SessionId),
            StudentId = StudentId,
            Speed = Speed,
            Accuracy = Accuracy,
            Anomalies = Anomalies,
            SubmittedAt = DateTime.Parse(SubmittedAt, null, System.Globalization.DateTimeStyles.RoundtripKind),
            AttemptIndex = AttemptIndex <= 0 ? 1 : AttemptIndex
        };
    }
}
