using System.Collections.Generic;
using System.Linq;
using Larpx.PersonalTools.TypeU.Models.Entities;

namespace Larpx.PersonalTools.TypeU.Services.Teacher;

/// <summary>
/// 最高成绩比较：正确率优先，相同再比速度。
/// </summary>
public static class BestScoreSelector
{
    /// <summary>
    /// 从多次成绩中选出最高的一次；无记录返回 null。
    /// </summary>
    public static ExamRecord? SelectBest(IEnumerable<ExamRecord> records)
    {
        return records
            .OrderByDescending(r => r.Accuracy)
            .ThenByDescending(r => r.Speed)
            .FirstOrDefault();
    }

    /// <summary>
    /// 格式化最高成绩展示文本。
    /// </summary>
    public static string FormatBest(ExamRecord? best)
    {
        if (best is null)
        {
            return string.Empty;
        }

        return $"第{best.AttemptIndex}次 {best.Speed:F1}字/分 / {best.Accuracy:F1}%";
    }
}
