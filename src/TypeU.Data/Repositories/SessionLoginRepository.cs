using System;
using System.Collections.Generic;
using System.Linq;
using Larpx.PersonalTools.TypeU.Models.Entities;
using SqlSugar;

namespace Larpx.PersonalTools.TypeU.Data.Repositories;

/// <summary>
/// 考试会话登录名单仓储。
/// </summary>
public sealed class SessionLoginRepository : RepositoryBase
{
    /// <summary>
    /// 初始化仓储。
    /// </summary>
    public SessionLoginRepository(SqliteConnectionFactory factory) : base(factory)
    {
    }

    /// <summary>
    /// 写入或更新登录记录。
    /// </summary>
    public void Upsert(SessionLogin login)
    {
        if (login is null)
        {
            throw new ArgumentNullException(nameof(login));
        }

        using var db = CreateClient();
        db.Ado.ExecuteCommand(
            """
            INSERT INTO SessionLogins (SessionId, DeviceFingerprint, StudentId, Name, LoggedInAt, LogoutAllowed)
            VALUES (@SessionId, @DeviceFingerprint, @StudentId, @Name, @LoggedInAt, @LogoutAllowed)
            ON CONFLICT(SessionId, DeviceFingerprint) DO UPDATE SET
                StudentId = excluded.StudentId,
                Name = excluded.Name,
                LoggedInAt = excluded.LoggedInAt,
                LogoutAllowed = excluded.LogoutAllowed;
            """,
            Row.FromEntity(login));
    }

    /// <summary>
    /// 按会话+设备指纹查询。
    /// </summary>
    public SessionLogin? GetByDevice(Guid sessionId, string deviceFingerprint)
    {
        using var db = CreateClient();
        var row = db.Ado.SqlQuerySingle<Row>(
            """
            SELECT SessionId, DeviceFingerprint, StudentId, Name, LoggedInAt, LogoutAllowed
            FROM SessionLogins WHERE SessionId = @SessionId AND DeviceFingerprint = @Fp;
            """,
            new { SessionId = sessionId.ToString("D"), Fp = deviceFingerprint });
        return row?.ToEntity();
    }

    /// <summary>
    /// 列出某会话全部登录。
    /// </summary>
    public IReadOnlyList<SessionLogin> GetBySession(Guid sessionId)
    {
        using var db = CreateClient();
        var rows = db.Ado.SqlQuery<Row>(
            """
            SELECT SessionId, DeviceFingerprint, StudentId, Name, LoggedInAt, LogoutAllowed
            FROM SessionLogins WHERE SessionId = @SessionId ORDER BY LoggedInAt;
            """,
            new { SessionId = sessionId.ToString("D") });
        return rows.Select(r => r.ToEntity()).ToList();
    }

    /// <summary>
    /// 设置是否允许登出。
    /// </summary>
    public void SetLogoutAllowed(Guid sessionId, string studentId, bool allowed)
    {
        using var db = CreateClient();
        db.Ado.ExecuteCommand(
            """
            UPDATE SessionLogins SET LogoutAllowed = @Allowed
            WHERE SessionId = @SessionId AND StudentId = @StudentId;
            """,
            new
            {
                SessionId = sessionId.ToString("D"),
                StudentId = studentId,
                Allowed = allowed ? 1 : 0
            });
    }

    /// <summary>
    /// 删除登录记录（学生登出后清除会话绑定）。
    /// </summary>
    public void Delete(Guid sessionId, string deviceFingerprint)
    {
        using var db = CreateClient();
        db.Ado.ExecuteCommand(
            "DELETE FROM SessionLogins WHERE SessionId = @SessionId AND DeviceFingerprint = @Fp;",
            new { SessionId = sessionId.ToString("D"), Fp = deviceFingerprint });
    }

    private sealed class Row
    {
        public string SessionId { get; set; } = string.Empty;
        public string DeviceFingerprint { get; set; } = string.Empty;
        public string StudentId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string LoggedInAt { get; set; } = string.Empty;
        public int LogoutAllowed { get; set; }

        public static Dictionary<string, object> FromEntity(SessionLogin e) => new()
        {
            ["SessionId"] = e.SessionId.ToString("D"),
            ["DeviceFingerprint"] = e.DeviceFingerprint,
            ["StudentId"] = e.StudentId,
            ["Name"] = e.Name,
            ["LoggedInAt"] = e.LoggedInAt.ToString("O"),
            ["LogoutAllowed"] = e.LogoutAllowed ? 1 : 0
        };

        public SessionLogin ToEntity() => new()
        {
            SessionId = Guid.Parse(SessionId),
            DeviceFingerprint = DeviceFingerprint,
            StudentId = StudentId,
            Name = Name,
            LoggedInAt = DateTime.Parse(LoggedInAt, null, System.Globalization.DateTimeStyles.RoundtripKind),
            LogoutAllowed = LogoutAllowed != 0
        };
    }
}
