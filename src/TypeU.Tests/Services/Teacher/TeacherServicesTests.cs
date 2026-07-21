using System;
using System.IO;
using System.Linq;
using Larpx.PersonalTools.TypeU.Data;
using Larpx.PersonalTools.TypeU.Data.Repositories;
using Larpx.PersonalTools.TypeU.Models.Entities;
using Larpx.PersonalTools.TypeU.Models.Enums;
using Larpx.PersonalTools.TypeU.Services.Teacher;
using StudentEntity = Larpx.PersonalTools.TypeU.Models.Entities.Student;
using Xunit;

namespace Larpx.PersonalTools.TypeU.Tests.Services.Teacher;

/// <summary>
/// 教师端业务服务测试（题库/绑定/成绩/监控）。
/// </summary>
public sealed class TeacherServicesTests : IDisposable
{
    private readonly string _dbPath;
    private readonly SqliteConnectionFactory _factory;

    public TeacherServicesTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"typeu-teacher-{Guid.NewGuid():N}.db");
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
    /// TXT 导入应按 [中文]/[英文]/[代码] 标记分类，无标记默认中文。
    /// </summary>
    [Fact]
    public void QuestionService_ImportFromTxt_ParsesTypes()
    {
        var repo = new QuestionRepository(_factory);
        var svc = new QuestionService(repo);

        var txtPath = Path.GetTempFileName();
        File.WriteAllLines(txtPath, new[]
        {
            "[中文] 床前明月光，疑是地上霜。",
            "[英文] The quick brown fox jumps over the lazy dog.",
            "[代码] Console.WriteLine(\"hello\");",
            "无标记行默认中文",
            "",
            "   "
        });

        var result = svc.ImportFromTxt(txtPath);
        File.Delete(txtPath);

        Assert.Equal(4, result.Imported);
        Assert.Equal(2, result.Skipped);

        var all = repo.GetAll();
        Assert.Equal(4, all.Count);
        Assert.Equal(QuestionType.Chinese, all.Single(q => q.Content.Contains("床前明月光")).Type);
        Assert.Equal(QuestionType.English, all.Single(q => q.Content.Contains("quick brown")).Type);
        Assert.Equal(QuestionType.Code, all.Single(q => q.Content.Contains("Console")).Type);
        Assert.Equal(QuestionType.Chinese, all.Single(q => q.Content == "无标记行默认中文").Type);
    }

    /// <summary>
    /// 绑定服务：新学号首次绑定应成功，2 小时内同设备允许，不同设备拒绝。
    /// </summary>
    [Fact]
    public void DeviceBinding_NewStudent_BindsSuccessfully()
    {
        var repo = new StudentRepository(_factory);
        var svc = new DeviceBindingService(repo);

        svc.Bind("S001", "张三", "FP-A");
        var (bound, fp) = svc.IsBoundToOtherDevice("S001", "FP-A");
        Assert.False(bound);
        Assert.Equal("FP-A", fp);
    }

    /// <summary>
    /// 绑定服务：已绑定其他未过期设备时应返回 bound=true。
    /// </summary>
    [Fact]
    public void DeviceBinding_DifferentDevice_ReturnsBound()
    {
        var repo = new StudentRepository(_factory);
        var svc = new DeviceBindingService(repo);

        svc.Bind("S002", "李四", "FP-A");
        var (bound, fp) = svc.IsBoundToOtherDevice("S002", "FP-B");

        Assert.True(bound);
        Assert.Equal("FP-A", fp);
    }

    /// <summary>
    /// 强制解绑后应允许新设备登录。
    /// </summary>
    [Fact]
    public void DeviceBinding_UnbindThenRebind_NewDeviceAllowed()
    {
        var repo = new StudentRepository(_factory);
        var svc = new DeviceBindingService(repo);

        svc.Bind("S003", "王五", "FP-A");
        svc.Unbind("S003");

        var (bound, _) = svc.IsBoundToOtherDevice("S003", "FP-B");
        Assert.False(bound);

        svc.Bind("S003", "王五", "FP-B");
        var (boundAfter, fpAfter) = svc.IsBoundToOtherDevice("S003", "FP-B");
        Assert.False(boundAfter);
        Assert.Equal("FP-B", fpAfter);
    }

    /// <summary>
    /// 剩余时长应随时间递减（2 小时绑定后初始剩余约 7200 秒）。
    /// </summary>
    [Fact]
    public void DeviceBinding_RemainingSeconds_AboutTwoHours()
    {
        var repo = new StudentRepository(_factory);
        var svc = new DeviceBindingService(repo);
        svc.Bind("S004", "赵六", "FP-X");

        var remaining = svc.GetRemainingSeconds("S004");
        Assert.InRange(remaining, 7100, 7200);
    }

    /// <summary>
    /// 监控服务：注册学生、更新进度、异常预警事件应触发。
    /// </summary>
    [Fact]
    public void Monitoring_UpdateProgress_TriggersAnomalyAlert()
    {
        var svc = new MonitoringService();
        string? alertStudent = null;
        string? alertReason = null;
        svc.AnomalyAlert += (sid, reason) =>
        {
            alertStudent = sid;
            alertReason = reason;
        };

        svc.RegisterStudent("S001", "张三", "192.168.1.10");
        svc.UpdateProgress("S001", speed: 60, accuracy: 95, progress: 100, anomalyCount: 0);
        Assert.Null(alertStudent);

        svc.UpdateProgress("S001", speed: 70, accuracy: 90, progress: 200, anomalyCount: 3);
        Assert.Equal("S001", alertStudent);
        Assert.Contains("3 次", alertReason);

        var states = svc.GetAllStates();
        Assert.Single(states);
        Assert.Equal(StudentStatus.Anomaly, states[0].Status);
    }

    /// <summary>
    /// ResetAll 后所有学生状态应回到 Online 且统计数据清零。
    /// </summary>
    [Fact]
    public void Monitoring_ResetAll_ClearsProgress()
    {
        var svc = new MonitoringService();
        svc.RegisterStudent("S001", "张三", "ip1");
        svc.UpdateProgress("S001", 50, 80, 100, 2);

        svc.ResetAll();

        var state = svc.GetAllStates().Single();
        Assert.Equal(StudentStatus.Online, state.Status);
        Assert.Equal(0, state.Speed);
        Assert.Equal(0, state.AnomalyCount);
    }

    /// <summary>
    /// GradeService Excel 导出应生成 xlsx 文件且可读取首行表头。
    /// </summary>
    [Fact]
    public void GradeService_ExportToExcel_GeneratesXlsx()
    {
        var examRepo = new ExamRepository(_factory);
        var studentRepo = new StudentRepository(_factory);
        var svc = new GradeService(examRepo, studentRepo);

        // 准备：学生 + 会话 + 成绩。
        studentRepo.Insert(new StudentEntity
        {
            StudentId = "S001",
            Name = "张三",
            DeviceFingerprint = "FP",
            BoundAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(2),
            Status = StudentStatus.Submitted
        });

        var sessionId = Guid.NewGuid();
        examRepo.InsertSession(new ExamSession
        {
            SessionId = sessionId,
            Mode = ExamMode.FixedLength,
            QuestionId = Guid.NewGuid(),
            StartedAt = DateTime.UtcNow,
            EndedAt = DateTime.UtcNow,
            Duration = 300
        });
        examRepo.InsertRecord(new ExamRecord
        {
            RecordId = Guid.NewGuid(),
            SessionId = sessionId,
            StudentId = "S001",
            Speed = 65.5,
            Accuracy = 95.0,
            Anomalies = "[]",
            SubmittedAt = DateTime.UtcNow
        });

        var outputPath = Path.Combine(Path.GetTempPath(), $"grades-{Guid.NewGuid():N}.xlsx");
        try
        {
            svc.ExportToExcel(sessionId, outputPath);
            Assert.True(File.Exists(outputPath));
            Assert.True(new FileInfo(outputPath).Length > 0);

            // 验证行数：用 MiniExcel 读取。
            using var stream = File.OpenRead(outputPath);
            var rows = MiniExcelLibs.MiniExcel.Query(stream).ToList();
            Assert.True(rows.Count >= 2, "至少包含表头 + 一行数据");
        }
        finally
        {
            try { File.Delete(outputPath); }
            catch { /* 忽略。 */ }
        }
    }
}
