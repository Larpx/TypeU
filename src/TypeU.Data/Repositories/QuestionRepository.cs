using System;
using System.Collections.Generic;
using System.Linq;
using Larpx.PersonalTools.TypeU.Models.Entities;
using Larpx.PersonalTools.TypeU.Models.Enums;
using SqlSugar;

namespace Larpx.PersonalTools.TypeU.Data.Repositories;

/// <summary>
/// 试题仓储：题库 CRUD，按类型过滤。
/// </summary>
public sealed class QuestionRepository : RepositoryBase
{
    /// <summary>
    /// 初始化仓储。
    /// </summary>
    /// <param name="factory">连接工厂。</param>
    public QuestionRepository(SqliteConnectionFactory factory) : base(factory)
    {
    }

    /// <summary>
    /// 根据试题 ID 查询。
    /// </summary>
    /// <param name="questionId">试题 ID。</param>
    /// <returns>试题实体；不存在时为 null。</returns>
    public Question? GetById(Guid questionId)
    {
        using var db = CreateClient();
        var row = db.Ado.SqlQuerySingle<QuestionRow>(
            "SELECT QuestionId, Type, Content, CreatedAt, ExpectedContent FROM Questions WHERE QuestionId = @QuestionId;",
            new { QuestionId = questionId.ToString("D") });
        return row?.ToEntity();
    }

    /// <summary>
    /// 获取全部试题。
    /// </summary>
    /// <returns>按创建时间倒序的试题列表。</returns>
    public IReadOnlyList<Question> GetAll()
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT QuestionId, Type, Content, CreatedAt, ExpectedContent FROM Questions ORDER BY CreatedAt DESC;";
        using var reader = cmd.ExecuteReader();

        var list = new List<Question>();
        while (reader.Read())
        {
            var row = new QuestionRow
            {
                QuestionId = reader.GetString(0),
                Type = reader.GetInt32(1),
                Content = reader.GetString(2),
                CreatedAt = reader.GetString(3),
                ExpectedContent = reader.IsDBNull(4) ? string.Empty : reader.GetString(4)
            };
            list.Add(row.ToEntity());
        }
        return list;
    }

    /// <summary>
    /// 按类型筛选试题。
    /// </summary>
    /// <param name="type">试题类型。</param>
    /// <returns>匹配类型的试题列表。</returns>
    public IReadOnlyList<Question> GetByType(QuestionType type)
    {
        using var db = CreateClient();
        var rows = db.Ado.SqlQuery<QuestionRow>(
            "SELECT QuestionId, Type, Content, CreatedAt, ExpectedContent FROM Questions WHERE Type = @Type ORDER BY CreatedAt DESC;",
            new { Type = (int)type });
        return rows.Select(r => r.ToEntity()).ToList();
    }

    /// <summary>
    /// 插入试题。
    /// </summary>
    /// <param name="question">试题实体。</param>
    public void Insert(Question question)
    {
        if (question is null)
        {
            throw new ArgumentNullException(nameof(question));
        }

        using var db = CreateClient();
        db.Ado.ExecuteCommand(
            """
            INSERT INTO Questions (QuestionId, Type, Content, CreatedAt, ExpectedContent)
            VALUES (@QuestionId, @Type, @Content, @CreatedAt, @ExpectedContent);
            """,
            QuestionRow.FromEntity(question));
    }

    /// <summary>
    /// 更新试题。
    /// </summary>
    /// <param name="question">试题实体。</param>
    public void Update(Question question)
    {
        if (question is null)
        {
            throw new ArgumentNullException(nameof(question));
        }

        using var db = CreateClient();
        db.Ado.ExecuteCommand(
            """
            UPDATE Questions
            SET Type = @Type, Content = @Content, ExpectedContent = @ExpectedContent
            WHERE QuestionId = @QuestionId;
            """,
            QuestionRow.FromEntity(question));
    }

    /// <summary>
    /// 删除试题。
    /// </summary>
    /// <param name="questionId">试题 ID。</param>
    public void Delete(Guid questionId)
    {
        using var db = CreateClient();
        db.Ado.ExecuteCommand(
            "DELETE FROM Questions WHERE QuestionId = @QuestionId;",
            new { QuestionId = questionId.ToString("D") });
    }

    private sealed class QuestionRow
    {
        public string QuestionId { get; set; } = string.Empty;
        public int Type { get; set; }
        public string Content { get; set; } = string.Empty;
        public string CreatedAt { get; set; } = string.Empty;
        public string ExpectedContent { get; set; } = string.Empty;

        public static Dictionary<string, object> FromEntity(Question e) => new()
        {
            ["QuestionId"] = e.QuestionId.ToString("D"),
            ["Type"] = (int)e.Type,
            ["Content"] = e.Content,
            ["CreatedAt"] = e.CreatedAt.ToString("O"),
            ["ExpectedContent"] = e.ExpectedContent ?? string.Empty
        };

        public Question ToEntity() => new()
        {
            QuestionId = Guid.Parse(QuestionId),
            Type = (QuestionType)Type,
            Content = Content,
            CreatedAt = DateTime.Parse(CreatedAt, null, System.Globalization.DateTimeStyles.RoundtripKind),
            ExpectedContent = ExpectedContent ?? string.Empty
        };
    }
}
