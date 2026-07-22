using System;
using System.IO;
using Larpx.PersonalTools.TypeU.Models.Dtos;
using Larpx.PersonalTools.TypeU.Models.Entities;
using Larpx.PersonalTools.TypeU.Models.Enums;
using ProtoBuf;
using Xunit;

namespace Larpx.PersonalTools.TypeU.Tests.Models;

/// <summary>
/// protobuf-net 序列化往返测试。
/// </summary>
public class SerializationTests
{
    /// <summary>
    /// 验证 Student 实体可往返序列化。
    /// </summary>
    [Fact]
    public void Student_RoundTrip_PreservesAllFields()
    {
        var expected = new Student
        {
            StudentId = "20260001",
            Name = "张三",
            DeviceFingerprint = "CPU-MAC-HDD-HASH-001",
            BoundAt = new DateTime(2026, 7, 21, 10, 0, 0, DateTimeKind.Utc),
            ExpiresAt = new DateTime(2026, 7, 21, 12, 0, 0, DateTimeKind.Utc),
            Status = StudentStatus.Examining
        };

        var actual = RoundTrip(expected);

        Assert.Equal(expected.StudentId, actual.StudentId);
        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.DeviceFingerprint, actual.DeviceFingerprint);
        Assert.Equal(expected.BoundAt, actual.BoundAt);
        Assert.Equal(expected.ExpiresAt, actual.ExpiresAt);
        Assert.Equal(expected.Status, actual.Status);
    }

    /// <summary>
    /// 验证 Question 实体可往返序列化。
    /// </summary>
    [Fact]
    public void Question_RoundTrip_PreservesAllFields()
    {
        var expected = new Question
        {
            QuestionId = Guid.NewGuid(),
            Type = QuestionType.Code,
            Content = "public class Hello { public void Say() => Console.WriteLine(\"hi\"); }",
            CreatedAt = new DateTime(2026, 7, 21, 9, 30, 0, DateTimeKind.Utc)
        };

        var actual = RoundTrip(expected);

        Assert.Equal(expected.QuestionId, actual.QuestionId);
        Assert.Equal(expected.Type, actual.Type);
        Assert.Equal(expected.Content, actual.Content);
        Assert.Equal(expected.CreatedAt, actual.CreatedAt);
    }

    /// <summary>
    /// 验证 ExamSession 实体可往返序列化。
    /// </summary>
    [Fact]
    public void ExamSession_RoundTrip_PreservesAllFields()
    {
        var expected = new ExamSession
        {
            SessionId = Guid.NewGuid(),
            Mode = ExamMode.TimedSprint,
            QuestionId = Guid.NewGuid(),
            StartedAt = new DateTime(2026, 7, 21, 10, 0, 0, DateTimeKind.Utc),
            EndedAt = new DateTime(2026, 7, 21, 10, 10, 0, DateTimeKind.Utc),
            Duration = 600
        };

        var actual = RoundTrip(expected);

        Assert.Equal(expected.SessionId, actual.SessionId);
        Assert.Equal(expected.Mode, actual.Mode);
        Assert.Equal(expected.QuestionId, actual.QuestionId);
        Assert.Equal(expected.StartedAt, actual.StartedAt);
        Assert.Equal(expected.EndedAt, actual.EndedAt);
        Assert.Equal(expected.Duration, actual.Duration);
    }

    /// <summary>
    /// 验证 ExamRecord 实体可往返序列化。
    /// </summary>
    [Fact]
    public void ExamRecord_RoundTrip_PreservesAllFields()
    {
        var expected = new ExamRecord
        {
            RecordId = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            StudentId = "20260002",
            Speed = 78.5,
            Accuracy = 96.3,
            Anomalies = "[\"批量上屏\",\"速度异常\"]",
            SubmittedAt = new DateTime(2026, 7, 21, 10, 10, 30, DateTimeKind.Utc)
        };

        var actual = RoundTrip(expected);

        Assert.Equal(expected.RecordId, actual.RecordId);
        Assert.Equal(expected.SessionId, actual.SessionId);
        Assert.Equal(expected.StudentId, actual.StudentId);
        Assert.Equal(expected.Speed, actual.Speed);
        Assert.Equal(expected.Accuracy, actual.Accuracy);
        Assert.Equal(expected.Anomalies, actual.Anomalies);
        Assert.Equal(expected.SubmittedAt, actual.SubmittedAt);
    }

    /// <summary>
    /// 验证 NonceCache 实体可往返序列化。
    /// </summary>
    [Fact]
    public void NonceCache_RoundTrip_PreservesAllFields()
    {
        var expected = new NonceCache
        {
            Nonce = "nonce-abc-1234567890",
            ReceivedAt = new DateTime(2026, 7, 21, 10, 0, 0, DateTimeKind.Utc)
        };

        var actual = RoundTrip(expected);

        Assert.Equal(expected.Nonce, actual.Nonce);
        Assert.Equal(expected.ReceivedAt, actual.ReceivedAt);
    }

    /// <summary>
    /// 验证 LoginDto 可往返序列化。
    /// </summary>
    [Fact]
    public void LoginDto_RoundTrip_PreservesAllFields()
    {
        var expected = new LoginDto
        {
            StudentId = "20260003",
            Name = "李四",
            DeviceFingerprint = "FP-LI-001",
            ComputerName = "ROOM1-PC-12",
            ClientIp = "192.168.1.23"
        };

        var actual = RoundTrip(expected);

        Assert.Equal(expected.StudentId, actual.StudentId);
        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.DeviceFingerprint, actual.DeviceFingerprint);
        Assert.Equal(expected.ComputerName, actual.ComputerName);
        Assert.Equal(expected.ClientIp, actual.ClientIp);
    }

    /// <summary>
    /// 验证 QuestionDto 可往返序列化。
    /// </summary>
    [Fact]
    public void QuestionDto_RoundTrip_PreservesAllFields()
    {
        var expected = new QuestionDto
        {
            QuestionId = Guid.NewGuid(),
            Type = QuestionType.Chinese,
            Content = "床前明月光，疑是地上霜。",
            ExpectedContent = "床前明月光，疑是地上霜。",
            Mode = ExamMode.FixedLength,
            Duration = 300,
            SessionId = Guid.NewGuid()
        };

        var actual = RoundTrip(expected);

        Assert.Equal(expected.QuestionId, actual.QuestionId);
        Assert.Equal(expected.Type, actual.Type);
        Assert.Equal(expected.Content, actual.Content);
        Assert.Equal(expected.ExpectedContent, actual.ExpectedContent);
        Assert.Equal(expected.Mode, actual.Mode);
        Assert.Equal(expected.Duration, actual.Duration);
        Assert.Equal(expected.SessionId, actual.SessionId);
    }

    /// <summary>
    /// 验证 ExamResultDto 可往返序列化。
    /// </summary>
    [Fact]
    public void ExamResultDto_RoundTrip_PreservesAllFields()
    {
        var expected = new ExamResultDto
        {
            RecordId = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            StudentId = "20260004",
            Speed = 65.2,
            Accuracy = 92.0,
            Anomalies = "[]",
            SubmittedAt = new DateTime(2026, 7, 21, 11, 5, 0, DateTimeKind.Utc),
            AttemptIndex = 2
        };

        var actual = RoundTrip(expected);

        Assert.Equal(expected.RecordId, actual.RecordId);
        Assert.Equal(expected.SessionId, actual.SessionId);
        Assert.Equal(expected.StudentId, actual.StudentId);
        Assert.Equal(expected.Speed, actual.Speed);
        Assert.Equal(expected.Accuracy, actual.Accuracy);
        Assert.Equal(expected.Anomalies, actual.Anomalies);
        Assert.Equal(expected.SubmittedAt, actual.SubmittedAt);
        Assert.Equal(expected.AttemptIndex, actual.AttemptIndex);
    }

    /// <summary>
    /// 验证 HelloAckDto / LoginAckDto 扩展字段可往返。
    /// </summary>
    [Fact]
    public void HelloAndLoginAck_RoundTrip_PreservesNewFields()
    {
        var hello = RoundTrip(new HelloAckDto
        {
            ExamRunning = true,
            AutoLogin = true,
            StudentId = "S001",
            StudentName = "张三",
            SessionId = Guid.NewGuid(),
            MaxAttempts = 3,
            AllowPracticeAfterSubmit = true,
            LogoutAllowed = false,
            ServerTimestampMs = 123456
        });
        Assert.True(hello.ExamRunning);
        Assert.True(hello.AutoLogin);
        Assert.Equal(3, hello.MaxAttempts);

        var login = RoundTrip(new LoginAckDto
        {
            Success = true,
            LogoutLocked = true,
            SessionId = Guid.NewGuid(),
            MaxAttempts = 2,
            AllowPracticeAfterSubmit = false,
            ServerTimestampMs = 999
        });
        Assert.True(login.LogoutLocked);
        Assert.Equal(2, login.MaxAttempts);
    }

    /// <summary>
    /// 验证 StatusReportDto 可往返序列化。
    /// </summary>
    [Fact]
    public void StatusReportDto_RoundTrip_PreservesAllFields()
    {
        var expected = new StatusReportDto
        {
            StudentId = "20260005",
            SessionId = Guid.NewGuid(),
            Speed = 42.8,
            Accuracy = 88.1,
            Progress = 120,
            TotalChars = 500,
            AnomalyCount = 2,
            Timestamp = new DateTime(2026, 7, 21, 10, 5, 30, DateTimeKind.Utc)
        };

        var actual = RoundTrip(expected);

        Assert.Equal(expected.StudentId, actual.StudentId);
        Assert.Equal(expected.SessionId, actual.SessionId);
        Assert.Equal(expected.Speed, actual.Speed);
        Assert.Equal(expected.Accuracy, actual.Accuracy);
        Assert.Equal(expected.Progress, actual.Progress);
        Assert.Equal(expected.TotalChars, actual.TotalChars);
        Assert.Equal(expected.AnomalyCount, actual.AnomalyCount);
        Assert.Equal(expected.Timestamp, actual.Timestamp);
    }

    /// <summary>
    /// 验证枚举类型的序列化往返。
    /// </summary>
    [Theory]
    [InlineData(QuestionType.Chinese)]
    [InlineData(QuestionType.English)]
    [InlineData(QuestionType.Code)]
    public void QuestionType_AllValues_RoundTrip(QuestionType type)
    {
        var expected = new Question
        {
            QuestionId = Guid.NewGuid(),
            Type = type,
            Content = "x",
            CreatedAt = DateTime.UtcNow
        };

        var actual = RoundTrip(expected);

        Assert.Equal(type, actual.Type);
    }

    /// <summary>
    /// 验证考试模式枚举的序列化往返。
    /// </summary>
    [Theory]
    [InlineData(ExamMode.FixedLength)]
    [InlineData(ExamMode.TimedSprint)]
    [InlineData(ExamMode.ErrorCorrection)]
    public void ExamMode_AllValues_RoundTrip(ExamMode mode)
    {
        var expected = new ExamSession
        {
            SessionId = Guid.NewGuid(),
            Mode = mode,
            QuestionId = Guid.NewGuid(),
            StartedAt = DateTime.UtcNow,
            EndedAt = DateTime.UtcNow,
            Duration = 100
        };

        var actual = RoundTrip(expected);

        Assert.Equal(mode, actual.Mode);
    }

    /// <summary>
    /// 验证学生状态枚举的序列化往返。
    /// </summary>
    [Theory]
    [InlineData(StudentStatus.Offline)]
    [InlineData(StudentStatus.Online)]
    [InlineData(StudentStatus.Examining)]
    [InlineData(StudentStatus.Submitted)]
    [InlineData(StudentStatus.Anomaly)]
    public void StudentStatus_AllValues_RoundTrip(StudentStatus status)
    {
        var expected = new Student
        {
            StudentId = "s1",
            Name = "n",
            DeviceFingerprint = "f",
            BoundAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow,
            Status = status
        };

        var actual = RoundTrip(expected);

        Assert.Equal(status, actual.Status);
    }

    /// <summary>
    /// 序列化后立即反序列化，确保字段完整。
    /// </summary>
    private static T RoundTrip<T>(T expected) where T : class
    {
        using var ms = new MemoryStream();
        Serializer.Serialize(ms, expected);
        ms.Position = 0;
        return Serializer.Deserialize<T>(ms);
    }
}
