using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Larpx.PersonalTools.TypeU.Data.Repositories;
using Larpx.PersonalTools.TypeU.Models.Entities;
using MiniExcelLibs;
using MiniExcelLibs.Csv;

namespace Larpx.PersonalTools.TypeU.Services.Teacher;

/// <summary>
/// 成绩管理服务：按会话查询、导出 Excel（学号/姓名/速度/正确率/异常记录）。
/// </summary>
public sealed class GradeService
{
    private readonly ExamRepository _examRepository;
    private readonly StudentRepository _studentRepository;

    /// <summary>
    /// 初始化服务。
    /// </summary>
    public GradeService(ExamRepository examRepository, StudentRepository studentRepository)
    {
        _examRepository = examRepository ?? throw new ArgumentNullException(nameof(examRepository));
        _studentRepository = studentRepository ?? throw new ArgumentNullException(nameof(studentRepository));
    }

    /// <summary>
    /// 按会话查询成绩列表（含学生姓名）。
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
                Speed = r.Speed,
                Accuracy = r.Accuracy,
                Anomalies = r.Anomalies,
                SubmittedAt = r.SubmittedAt
            });
        }
        return rows;
    }

    /// <summary>
    /// 导出成绩到 Excel 文件（xlsx）。
    /// </summary>
    /// <param name="sessionId">会话 ID。</param>
    /// <param name="outputPath">输出 xlsx 路径。</param>
    public void ExportToExcel(Guid sessionId, string outputPath)
    {
        var rows = GetGradesBySession(sessionId);
        var dictRows = rows.Select(r => new Dictionary<string, object>
        {
            ["学号"] = r.StudentId,
            ["姓名"] = r.Name,
            ["速度(字/分钟)"] = r.Speed,
            ["正确率(%)"] = r.Accuracy,
            ["异常记录"] = r.Anomalies,
            ["提交时间"] = r.SubmittedAt.ToString("yyyy-MM-dd HH:mm:ss")
        }).ToList();

        MiniExcel.SaveAs(outputPath, dictRows);
    }

    /// <summary>
    /// 收卷：保存学生成绩到数据库。
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
/// 成绩报表行。
/// </summary>
public sealed class GradeReportRow
{
    /// <summary>学号。</summary>
    public string StudentId { get; set; } = string.Empty;

    /// <summary>姓名。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>速度（字/分钟）。</summary>
    public double Speed { get; set; }

    /// <summary>正确率（0-100）。</summary>
    public double Accuracy { get; set; }

    /// <summary>异常记录（JSON 字符串）。</summary>
    public string Anomalies { get; set; } = string.Empty;

    /// <summary>提交时间（UTC）。</summary>
    public DateTime SubmittedAt { get; set; }
}
