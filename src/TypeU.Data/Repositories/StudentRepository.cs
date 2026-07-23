using System;
using System.Collections.Generic;
using System.Linq;
using Larpx.PersonalTools.TypeU.Models.Entities;
using Larpx.PersonalTools.TypeU.Models.Enums;
using SqlSugar;

namespace Larpx.PersonalTools.TypeU.Data.Repositories;

/// <summary>
/// 学生仓储：维护 Students 表与设备绑定关系。
/// </summary>
public sealed class StudentRepository : RepositoryBase
{
    /// <summary>
    /// 初始化仓储。
    /// </summary>
    public StudentRepository(SqliteConnectionFactory factory) : base(factory)
    {
    }

    /// <summary>
    /// 根据学号查询学生。
    /// </summary>
    public Student? GetById(string studentId)
    {
        using var db = CreateClient();
        var row = db.Ado.SqlQuerySingle<StudentRow>(
            "SELECT StudentId, Name, DeviceFingerprint, BoundAt, ExpiresAt, Status FROM Students WHERE StudentId = @StudentId;",
            new { StudentId = studentId });
        return row?.ToEntity();
    }

    /// <summary>
    /// 获取全部学生。
    /// </summary>
    public IReadOnlyList<Student> GetAll()
    {
        using var db = CreateClient();
        var rows = db.Ado.SqlQuery<StudentRow>(
            "SELECT StudentId, Name, DeviceFingerprint, BoundAt, ExpiresAt, Status FROM Students ORDER BY StudentId;");
        return rows.Select(r => r.ToEntity()).ToList();
    }

    /// <summary>
    /// 插入学生记录。
    /// </summary>
    public void Insert(Student student)
    {
        if (student is null)
        {
            throw new ArgumentNullException(nameof(student));
        }

        using var db = CreateClient();
        db.Ado.ExecuteCommand(
            """
            INSERT INTO Students (StudentId, Name, DeviceFingerprint, BoundAt, ExpiresAt, Status)
            VALUES (@StudentId, @Name, @DeviceFingerprint, @BoundAt, @ExpiresAt, @Status);
            """,
            StudentRow.FromEntity(student));
    }

    /// <summary>
    /// 更新学生记录（含设备绑定信息）。
    /// </summary>
    public void Update(Student student)
    {
        if (student is null)
        {
            throw new ArgumentNullException(nameof(student));
        }

        using var db = CreateClient();
        db.Ado.ExecuteCommand(
            """
            UPDATE Students
            SET Name = @Name,
                DeviceFingerprint = @DeviceFingerprint,
                BoundAt = @BoundAt,
                ExpiresAt = @ExpiresAt,
                Status = @Status
            WHERE StudentId = @StudentId;
            """,
            StudentRow.FromEntity(student));
    }

    /// <summary>
    /// 更新学生在线状态。
    /// </summary>
    public void UpdateStatus(string studentId, StudentStatus status)
    {
        using var db = CreateClient();
        db.Ado.ExecuteCommand(
            "UPDATE Students SET Status = @Status WHERE StudentId = @StudentId;",
            new { StudentId = studentId, Status = (int)status });
    }

    /// <summary>
    /// 删除学生记录。
    /// </summary>
    public void Delete(string studentId)
    {
        using var db = CreateClient();
        db.Ado.ExecuteCommand("DELETE FROM Students WHERE StudentId = @StudentId;", new { StudentId = studentId });
    }

    /// <summary>
    /// 判断学号是否已绑定其他未过期的设备指纹。
    /// 返回值：null=学号不存在或未绑定；非 null=已绑定的设备指纹（若与传入 fingerprint 不同则拒绝登录）。
    /// </summary>
    public string? GetActiveBoundFingerprint(string studentId, DateTime nowUtc)
    {
        using var db = CreateClient();
        var row = db.Ado.SqlQuerySingle<StudentRow>(
            "SELECT StudentId, Name, DeviceFingerprint, BoundAt, ExpiresAt, Status FROM Students WHERE StudentId = @StudentId;",
            new { StudentId = studentId });

        if (row is null || string.IsNullOrEmpty(row.DeviceFingerprint))
        {
            return null;
        }

        var expiresAt = DateTime.Parse(row.ExpiresAt, null, System.Globalization.DateTimeStyles.RoundtripKind);
        if (expiresAt <= nowUtc)
        {
            return null;
        }

        return row.DeviceFingerprint;
    }

    /// <summary>
    /// 强制解绑设备（清空指纹与过期时间）。
    /// </summary>
    public void Unbind(string studentId)
    {
        using var db = CreateClient();
        db.Ado.ExecuteCommand(
            """
            UPDATE Students
            SET DeviceFingerprint = '',
                BoundAt = @BoundAt,
                ExpiresAt = @BoundAt
            WHERE StudentId = @StudentId;
            """,
            new { StudentId = studentId, BoundAt = DateTime.UtcNow.ToString("O") });
    }

    private sealed class StudentRow
    {
        public string StudentId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string DeviceFingerprint { get; set; } = string.Empty;
        public string BoundAt { get; set; } = string.Empty;
        public string ExpiresAt { get; set; } = string.Empty;
        public int Status { get; set; }

        public static Dictionary<string, object> FromEntity(Student e) => new()
        {
            ["StudentId"] = e.StudentId,
            ["Name"] = e.Name,
            ["DeviceFingerprint"] = e.DeviceFingerprint,
            ["BoundAt"] = e.BoundAt.ToString("O"),
            ["ExpiresAt"] = e.ExpiresAt.ToString("O"),
            ["Status"] = (int)e.Status
        };

        public Student ToEntity() => new()
        {
            StudentId = StudentId,
            Name = Name,
            DeviceFingerprint = DeviceFingerprint,
            BoundAt = DateTime.Parse(BoundAt, null, System.Globalization.DateTimeStyles.RoundtripKind),
            ExpiresAt = DateTime.Parse(ExpiresAt, null, System.Globalization.DateTimeStyles.RoundtripKind),
            Status = (StudentStatus)Status
        };
    }
}
