using System;
using System.IO;
using System.Linq;
using Larpx.PersonalTools.TypeU.Data;
using Larpx.PersonalTools.TypeU.Data.Repositories;
using Larpx.PersonalTools.TypeU.Models.Entities;
using Larpx.PersonalTools.TypeU.Models.Enums;
using Xunit;

namespace Larpx.PersonalTools.TypeU.Tests.Data;

/// <summary>
/// SQLite 仓储 CRUD 与设备绑定过期逻辑测试。
/// </summary>
public sealed class RepositoriesTests : IDisposable
{
    private readonly string _dbPath;
    private readonly SqliteConnectionFactory _factory;

    public RepositoriesTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"typeu-test-{Guid.NewGuid():N}.db");
        _factory = new SqliteConnectionFactory($"Data Source={_dbPath}");
        new DatabaseInitializer(_factory).Initialize();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        try { File.Delete(_dbPath); }
        catch { /* 忽略。 */ }
    }

    /// <summary>
    /// 建表脚本应可重复执行（幂等）。
    /// </summary>
    [Fact]
    public void Initialize_CalledTwice_DoesNotThrow()
    {
        var ex = Record.Exception(() => new DatabaseInitializer(_factory).Initialize());
        Assert.Null(ex);
    }

    /// <summary>
    /// 学生 CRUD 全流程。
    /// </summary>
    [Fact]
    public void Student_Crud_Works()
    {
        var repo = new StudentRepository(_factory);
        var student = new Student
        {
            StudentId = "S001",
            Name = "张三",
            DeviceFingerprint = "FP-001",
            BoundAt = new DateTime(2026, 7, 21, 10, 0, 0, DateTimeKind.Utc),
            ExpiresAt = new DateTime(2026, 7, 21, 12, 0, 0, DateTimeKind.Utc),
            Status = StudentStatus.Online
        };

        repo.Insert(student);

        var fetched = repo.GetById("S001");
        Assert.NotNull(fetched);
        Assert.Equal("张三", fetched!.Name);
        Assert.Equal("FP-001", fetched.DeviceFingerprint);
        Assert.Equal(StudentStatus.Online, fetched.Status);

        fetched.Name = "李四";
        fetched.Status = StudentStatus.Examining;
        repo.Update(fetched);

        var updated = repo.GetById("S001");
        Assert.NotNull(updated);
        Assert.Equal("李四", updated!.Name);
        Assert.Equal(StudentStatus.Examining, updated.Status);

        var all = repo.GetAll();
        Assert.Single(all);

        repo.Delete("S001");
        Assert.Null(repo.GetById("S001"));
    }

    /// <summary>
    /// 已绑定设备未过期时返回已绑定指纹。
    /// </summary>
    [Fact]
    public void GetActiveBoundFingerprint_NotExpired_ReturnsFingerprint()
    {
        var repo = new StudentRepository(_factory);
        var now = DateTime.UtcNow;
        repo.Insert(new Student
        {
            StudentId = "S002",
            Name = "王五",
            DeviceFingerprint = "FP-002",
            BoundAt = now,
            ExpiresAt = now.AddHours(2),
            Status = StudentStatus.Offline
        });

        var fp = repo.GetActiveBoundFingerprint("S002", now.AddMinutes(30));
        Assert.Equal("FP-002", fp);
    }

    /// <summary>
    /// 已绑定设备已过期时返回 null（允许重新绑定）。
    /// </summary>
    [Fact]
    public void GetActiveBoundFingerprint_Expired_ReturnsNull()
    {
        var repo = new StudentRepository(_factory);
        var now = DateTime.UtcNow;
        repo.Insert(new Student
        {
            StudentId = "S003",
            Name = "赵六",
            DeviceFingerprint = "FP-003",
            BoundAt = now.AddHours(-3),
            ExpiresAt = now.AddHours(-1),
            Status = StudentStatus.Offline
        });

        var fp = repo.GetActiveBoundFingerprint("S003", now);
        Assert.Null(fp);
    }

    /// <summary>
    /// 强制解绑后 GetActiveBoundFingerprint 应返回 null。
    /// </summary>
    [Fact]
    public void Unbind_ClearsFingerprint()
    {
        var repo = new StudentRepository(_factory);
        var now = DateTime.UtcNow;
        repo.Insert(new Student
        {
            StudentId = "S004",
            Name = "钱七",
            DeviceFingerprint = "FP-004",
            BoundAt = now,
            ExpiresAt = now.AddHours(2),
            Status = StudentStatus.Offline
        });

        repo.Unbind("S004");

        var fp = repo.GetActiveBoundFingerprint("S004", now);
        Assert.Null(fp);

        var student = repo.GetById("S004");
        Assert.NotNull(student);
        Assert.Equal(string.Empty, student!.DeviceFingerprint);
    }

    /// <summary>
    /// 题库 CRUD 与按类型过滤。
    /// </summary>
    [Fact]
    public void Question_Crud_AndFilter_Works()
    {
        var repo = new QuestionRepository(_factory);

        var q1 = new Question
        {
            QuestionId = Guid.NewGuid(),
            Type = QuestionType.Chinese,
            Content = "床前明月光",
            CreatedAt = DateTime.UtcNow
        };
        var q2 = new Question
        {
            QuestionId = Guid.NewGuid(),
            Type = QuestionType.Code,
            Content = "Console.WriteLine();",
            CreatedAt = DateTime.UtcNow
        };
        repo.Insert(q1);
        repo.Insert(q2);

        var all = repo.GetAll();
        Assert.Equal(2, all.Count);

        var codeOnly = repo.GetByType(QuestionType.Code);
        Assert.Single(codeOnly);
        Assert.Equal(q2.QuestionId, codeOnly[0].QuestionId);

        q1.Content = "更新内容";
        repo.Update(q1);
        Assert.Equal("更新内容", repo.GetById(q1.QuestionId)!.Content);

        repo.Delete(q2.QuestionId);
        Assert.Single(repo.GetAll());
    }

    /// <summary>
    /// 考试会话与成绩记录 CRUD。
    /// </summary>
    [Fact]
    public void Exam_SessionAndRecord_Works()
    {
        var repo = new ExamRepository(_factory);

        var sessionId = Guid.NewGuid();
        var questionId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var session = new ExamSession
        {
            SessionId = sessionId,
            Mode = ExamMode.TimedSprint,
            QuestionId = questionId,
            StartedAt = now,
            EndedAt = now.AddMinutes(10),
            Duration = 600
        };
        repo.InsertSession(session);

        var fetched = repo.GetSessionById(sessionId);
        Assert.NotNull(fetched);
        Assert.Equal(ExamMode.TimedSprint, fetched!.Mode);
        Assert.Equal(600, fetched.Duration);

        var records = new[]
        {
            new ExamRecord
            {
                RecordId = Guid.NewGuid(),
                SessionId = sessionId,
                StudentId = "S001",
                Speed = 55.5,
                Accuracy = 92.0,
                Anomalies = "[]",
                SubmittedAt = now.AddMinutes(10)
            },
            new ExamRecord
            {
                RecordId = Guid.NewGuid(),
                SessionId = sessionId,
                StudentId = "S002",
                Speed = 70.2,
                Accuracy = 95.0,
                Anomalies = "[\"批量上屏\"]",
                SubmittedAt = now.AddMinutes(10)
            }
        };

        foreach (var r in records)
        {
            repo.InsertRecord(r);
        }

        var bySession = repo.GetRecordsBySession(sessionId);
        Assert.Equal(2, bySession.Count);
        // 降序排列：Speed 高的在前。
        Assert.Equal(70.2, bySession[0].Speed);

        var byStudent = repo.GetRecordsByStudent("S001");
        Assert.Single(byStudent);
        Assert.Equal(55.5, byStudent[0].Speed);
    }

    /// <summary>
    /// Nonce 去重与过期清理。
    /// </summary>
    [Fact]
    public void Nonce_TryAdd_AndPurge_Works()
    {
        var repo = new NonceRepository(_factory);
        var now = DateTime.UtcNow;

        Assert.True(repo.TryAdd("nonce-A", now));
        Assert.False(repo.TryAdd("nonce-A", now));
        Assert.True(repo.TryAdd("nonce-B", now));

        Assert.Equal(2, repo.GetAll().Count);

        var purged = repo.PurgeOlderThan(now.AddSeconds(1));
        Assert.Equal(2, purged);
        Assert.Empty(repo.GetAll());
    }
}
