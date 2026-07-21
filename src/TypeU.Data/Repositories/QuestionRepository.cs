using System;
using System.Collections.Generic;
using System.Linq;
using Dapper;
using Larpx.PersonalTools.TypeU.Models.Entities;
using Larpx.PersonalTools.TypeU.Models.Enums;

namespace Larpx.PersonalTools.TypeU.Data.Repositories;

/// <summary>
/// 试题仓储：题库 CRUD，按类型过滤。
/// </summary>
public sealed class QuestionRepository : RepositoryBase
{
    /// <summary>
    /// 初始化仓储。
    /// </summary>
    public QuestionRepository(SqliteConnectionFactory factory) : base(factory)
    {
    }

    /// <summary>
    /// 根据试题 ID 查询。
    /// </summary>
    public Question? GetById(Guid questionId)
    {
        using var conn = OpenConnection();
        var row = conn.QueryFirstOrDefault<QuestionRow>(
            "SELECT QuestionId, Type, Content, CreatedAt FROM Questions WHERE QuestionId = @QuestionId;",
            new { QuestionId = questionId.ToString("D") });
        return row?.ToEntity();
    }

    /// <summary>
    /// 获取全部试题。
    /// </summary>
    public IReadOnlyList<Question> GetAll()
    {
        using var conn = OpenConnection();
        var rows = conn.Query<QuestionRow>(
            "SELECT QuestionId, Type, Content, CreatedAt FROM Questions ORDER BY CreatedAt DESC;");
        return rows.Select(r => r.ToEntity()).ToList();
    }

    /// <summary>
    /// 按类型筛选试题。
    /// </summary>
    public IReadOnlyList<Question> GetByType(QuestionType type)
    {
        using var conn = OpenConnection();
        var rows = conn.Query<QuestionRow>(
            "SELECT QuestionId, Type, Content, CreatedAt FROM Questions WHERE Type = @Type ORDER BY CreatedAt DESC;",
            new { Type = (int)type });
        return rows.Select(r => r.ToEntity()).ToList();
    }

    /// <summary>
    /// 插入试题。
    /// </summary>
    public void Insert(Question question)
    {
        if (question is null)
        {
            throw new ArgumentNullException(nameof(question));
        }

        using var conn = OpenConnection();
        conn.Execute(
            """
            INSERT INTO Questions (QuestionId, Type, Content, CreatedAt)
            VALUES (@QuestionId, @Type, @Content, @CreatedAt);
            """,
            QuestionRow.FromEntity(question));
    }

    /// <summary>
    /// 更新试题。
    /// </summary>
    public void Update(Question question)
    {
        if (question is null)
        {
            throw new ArgumentNullException(nameof(question));
        }

        using var conn = OpenConnection();
        conn.Execute(
            """
            UPDATE Questions
            SET Type = @Type, Content = @Content
            WHERE QuestionId = @QuestionId;
            """,
            QuestionRow.FromEntity(question));
    }

    /// <summary>
    /// 删除试题。
    /// </summary>
    public void Delete(Guid questionId)
    {
        using var conn = OpenConnection();
        conn.Execute(
            "DELETE FROM Questions WHERE QuestionId = @QuestionId;",
            new { QuestionId = questionId.ToString("D") });
    }

    private sealed class QuestionRow
    {
        public string QuestionId { get; set; } = string.Empty;
        public int Type { get; set; }
        public string Content { get; set; } = string.Empty;
        public string CreatedAt { get; set; } = string.Empty;

        public static QuestionRow FromEntity(Question e) => new()
        {
            QuestionId = e.QuestionId.ToString("D"),
            Type = (int)e.Type,
            Content = e.Content,
            CreatedAt = e.CreatedAt.ToString("O")
        };

        public Question ToEntity() => new()
        {
            QuestionId = Guid.Parse(QuestionId),
            Type = (QuestionType)Type,
            Content = Content,
            CreatedAt = DateTime.Parse(CreatedAt, null, System.Globalization.DateTimeStyles.RoundtripKind)
        };
    }
}
