using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Larpx.PersonalTools.TypeU.Data.Repositories;
using Larpx.PersonalTools.TypeU.Models.Entities;
using Larpx.PersonalTools.TypeU.Models.Enums;
using Microsoft.Extensions.Logging;

namespace Larpx.PersonalTools.TypeU.Services.Teacher;

/// <summary>
/// 题库管理服务：CRUD + TXT 导入。
/// TXT 导入格式：每行一道题，行首 [中文|英文|代码] 标记类型，缺省为中文。
/// 纠错题可用 <c>含错原文|||参考答案</c> 分隔（||| 后为 ExpectedContent）。
/// 示例：
///   [中文] 床前明月光，疑是地上霜。
///   [英文] The quick brown fox.
///   [代码] Console.WriteLine("hello");
///   [中文] 床前名月光|||床前明月光
/// </summary>
public sealed class QuestionService
{
    private readonly QuestionRepository _repository;
    private readonly ILogger<QuestionService>? _logger;

    /// <summary>
    /// 初始化服务。
    /// </summary>
    public QuestionService(QuestionRepository repository, ILogger<QuestionService>? logger = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger;
    }

    /// <summary>
    /// 获取全部试题。
    /// </summary>
    public IReadOnlyList<Question> GetAll() => _repository.GetAll();

    /// <summary>
    /// 按类型筛选。
    /// </summary>
    public IReadOnlyList<Question> GetByType(QuestionType type) => _repository.GetByType(type);

    /// <summary>
    /// 新增试题。
    /// </summary>
    /// <param name="type">试题类型。</param>
    /// <param name="content">试题正文（纠错模式为含错原文）。</param>
    /// <param name="expectedContent">参考答案（纠错模式必填，其它模式可空）。</param>
    public Question Add(QuestionType type, string content, string expectedContent = "")
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("试题内容不能为空。", nameof(content));
        }

        var question = new Question
        {
            QuestionId = Guid.NewGuid(),
            Type = type,
            Content = content,
            ExpectedContent = expectedContent ?? string.Empty,
            CreatedAt = DateTime.UtcNow
        };
        _repository.Insert(question);
        _logger?.LogInformation("新增试题 {QuestionId} 类型 {Type}", question.QuestionId, type);
        return question;
    }

    /// <summary>
    /// 更新试题内容。
    /// </summary>
    /// <param name="questionId">试题 ID。</param>
    /// <param name="type">试题类型。</param>
    /// <param name="content">试题正文。</param>
    /// <param name="expectedContent">参考答案。</param>
    public void Update(Guid questionId, QuestionType type, string content, string expectedContent = "")
    {
        var existing = _repository.GetById(questionId) ?? throw new InvalidOperationException("试题不存在。");
        existing.Type = type;
        existing.Content = content;
        existing.ExpectedContent = expectedContent ?? string.Empty;
        _repository.Update(existing);
    }

    /// <summary>
    /// 删除试题。
    /// </summary>
    public void Delete(Guid questionId)
    {
        _repository.Delete(questionId);
        _logger?.LogInformation("删除试题 {QuestionId}", questionId);
    }

    /// <summary>
    /// 从 TXT 文件批量导入试题。
    /// </summary>
    /// <param name="path">TXT 文件路径。</param>
    /// <returns>导入结果（成功数 + 跳过数）。</returns>
    public TxtImportResult ImportFromTxt(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("TXT 文件不存在。", path);
        }

        var lines = File.ReadAllLines(path);
        var imported = 0;
        var skipped = 0;

        foreach (var raw in lines)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                skipped++;
                continue;
            }

            var (type, content, expected) = ParseLine(raw);
            if (string.IsNullOrWhiteSpace(content))
            {
                skipped++;
                continue;
            }

            Add(type, content, expected);
            imported++;
        }

        _logger?.LogInformation("TXT 导入完成：成功 {Imported}，跳过 {Skipped}", imported, skipped);
        return new TxtImportResult(imported, skipped);
    }

    private static (QuestionType type, string content, string expected) ParseLine(string raw)
    {
        var trimmed = raw.Trim();
        QuestionType type;
        string body;
        if (trimmed.StartsWith("[中文]", StringComparison.Ordinal))
        {
            type = QuestionType.Chinese;
            body = trimmed.Substring(4).Trim();
        }
        else if (trimmed.StartsWith("[英文]", StringComparison.Ordinal))
        {
            type = QuestionType.English;
            body = trimmed.Substring(4).Trim();
        }
        else if (trimmed.StartsWith("[代码]", StringComparison.Ordinal))
        {
            type = QuestionType.Code;
            body = trimmed.Substring(4).Trim();
        }
        else
        {
            type = QuestionType.Chinese;
            body = trimmed;
        }

        var sep = body.IndexOf("|||", StringComparison.Ordinal);
        if (sep >= 0)
        {
            return (type, body.Substring(0, sep).Trim(), body.Substring(sep + 3).Trim());
        }

        return (type, body, string.Empty);
    }
}

/// <summary>
/// TXT 导入结果。
/// </summary>
public sealed record TxtImportResult(int Imported, int Skipped);
