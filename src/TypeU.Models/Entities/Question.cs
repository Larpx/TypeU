using System;
using Larpx.PersonalTools.TypeU.Models.Enums;
using ProtoBuf;

namespace Larpx.PersonalTools.TypeU.Models.Entities;

/// <summary>
/// 试题实体（对应 Questions 表）。
/// </summary>
[ProtoContract]
public sealed partial class Question
{
    /// <summary>
    /// 试题 ID（主键）。
    /// </summary>
    [ProtoMember(1)]
    public Guid QuestionId { get; set; }

    /// <summary>
    /// 试题类型（中文/英文/代码）。
    /// </summary>
    [ProtoMember(2)]
    public QuestionType Type { get; set; }

    /// <summary>
    /// 试题正文内容。
    /// </summary>
    [ProtoMember(3)]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 创建时间（UTC）。
    /// </summary>
    [ProtoMember(4)]
    public DateTime CreatedAt { get; set; }
}
