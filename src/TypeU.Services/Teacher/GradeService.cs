using System;
using System.Collections.Generic;
using System.Linq;
using Larpx.PersonalTools.TypeU.Data.Repositories;
using Larpx.PersonalTools.TypeU.Models.Entities;
using MiniExcelLibs;

namespace Larpx.PersonalTools.TypeU.Services.Teacher;

/// <summary>
/// 成绩管理：多试次查询与导出（含最高成绩列）。
/// </summary>
public sealed class GradeService
{
    private readonly ExamRepository _examRepository;
    private readonly StudentRepository _studentRepository;

    /// <summary>
    /// 初始化。
    /// </summary>
    public GradeService(ExamRepository examRepository, StudentRepository studentRepository)
    {
        _examRepository = examRepository ?? throw new ArgumentNullException(nameof(examRepository));
        _studentRepository = studentRepository ?? throw new ArgumentNullException(nameof(studentRepository));
    }

    /// <summary>
    /// 按会话查询扁平成绩行。
    /// </summary>
    public IReadOnlyList<GradeReportRow> GetGradesBySession(Guid sessionId)
    {
        var records = _examRepository.GetRecordsBySession(sessionId);
        var rows = new List<GradeReportRow>(records.Count);
        foreach (var r in records)
        {
            var student = _studentRepository.GetById(r.StudentId);
            rows.Add(new GradeReportRow
            {
                StudentId = r.StudentId,
                Name = student?.Name ?? string.Empty,
                AttemptIndex = r.AttemptIndex,
                Speed = r.Speed,
                Accuracy = r.Accuracy,
                Anomalies = r.Anomalies,
                SubmittedAt = r.SubmittedAt
            });
        }

        return rows;
    }

    /// <summary>
    /// 按学生聚合：各次成绩 + 最高成绩。
    /// </summary>
    public IReadOnlyList<GradeExportRow> GetExportRows(Guid sessionId, int maxAttempts)
    {
        var records = _examRepository.GetRecordsBySession(sessionId);
        var groups = records.GroupBy(r => r.StudentId);
        var list = new List<GradeExportRow>();
        foreach (var g in groups)
        {
            var student = _studentRepository.GetById(g.Key);
            var attempts = g.OrderBy(x => x.AttemptIndex).ToList();
            var best = BestScoreSelector.SelectBest(attempts);
            var row = new GradeExportRow
            {
                StudentId = g.Key,
                Name = student?.Name ?? attempts.FirstOrDefault()?.StudentId ?? string.Empty,
                BestScoreText = BestScoreSelector.FormatBest(best)
            };

            for (var i = 1; i <= maxAttempts; i++)
            {
                var a = attempts.FirstOrDefault(x => x.AttemptIndex == i);
                row.AttemptScores[i] = a is null
                    ? string.Empty
                    : $"{a.Speed:F1}/{a.Accuracy:F1}%";
            }

            list.Add(row);
        }

        return list;
    }

    /// <summary>
    /// 导出 Excel：各次成绩列 + 最高成绩。
    /// </summary>
    public void ExportToExcel(Guid sessionId, string outputPath)
    {
        var session = _examRepository.GetSessionById(sessionId);
        var maxAttempts = session?.MaxAttempts ?? 1;
        if (maxAttempts < 1)
        {
            maxAttempts = 1;
        }

        if (maxAttempts > 5)
        {
            maxAttempts = 5;
        }

        var rows = GetExportRows(sessionId, maxAttempts);
        var dictRows = new List<Dictionary<string, object>>();
        foreach (var r in rows)
        {
            var dict = new Dictionary<string, object>
            {
                ["学号"] = r.StudentId,
                ["姓名"] = r.Name
            };
            for (var i = 1; i <= maxAttempts; i++)
            {
                dict[$"第{i}次成绩"] = r.AttemptScores.TryGetValue(i, out var s) ? s : string.Empty;
            }

            dict["最高成绩"] = r.BestScoreText;
            dictRows.Add(dict);
        }

        MiniExcel.SaveAs(outputPath, dictRows);
    }

    /// <summary>
    /// 收卷入库。
    /// </summary>
    public void CollectRecord(ExamRecord record)
    {
        if (record is null)
        {
            throw new ArgumentNullException(nameof(record));
        }

        _examRepository.InsertRecord(record);
    }
}

/// <summary>
/// 单次成绩行。
/// </summary>
public sealed class GradeReportRow
{
    /// <summary>学号。</summary>
    public string StudentId { get; set; } = string.Empty;

    /// <summary>姓名。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>第几次。</summary>
    public int AttemptIndex { get; set; }

    /// <summary>速度。</summary>
    public double Speed { get; set; }

    /// <summary>正确率。</summary>
    public double Accuracy { get; set; }

    /// <summary>异常。</summary>
    public string Anomalies { get; set; } = string.Empty;

    /// <summary>提交时间。</summary>
    public DateTime SubmittedAt { get; set; }
}

/// <summary>
/// 导出聚合行。
/// </summary>
public sealed class GradeExportRow
{
    /// <summary>学号。</summary>
    public string StudentId { get; set; } = string.Empty;

    /// <summary>姓名。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>各次成绩文本。</summary>
    public Dictionary<int, string> AttemptScores { get; } = new();

    /// <summary>最高成绩文本。</summary>
    public string BestScoreText { get; set; } = string.Empty;
}
