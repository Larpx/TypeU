using System;
using System.Collections.Generic;
using Larpx.PersonalTools.TypeU.Models.Entities;
using Larpx.PersonalTools.TypeU.Services.Teacher;
using Xunit;

namespace Larpx.PersonalTools.TypeU.Tests.Services.Teacher;

/// <summary>
/// 最高成绩选择规则：正确率优先，相同再比速度。
/// </summary>
public sealed class BestScoreSelectorTests
{
    [Fact]
    public void SelectBest_PrefersHigherAccuracy()
    {
        var records = new List<ExamRecord>
        {
            Make(1, speed: 100, accuracy: 90),
            Make(2, speed: 120, accuracy: 95),
            Make(3, speed: 80, accuracy: 92)
        };

        var best = BestScoreSelector.SelectBest(records);
        Assert.NotNull(best);
        Assert.Equal(2, best!.AttemptIndex);
        Assert.Equal(95, best.Accuracy);
    }

    [Fact]
    public void SelectBest_WhenAccuracyEqual_PrefersHigherSpeed()
    {
        var records = new List<ExamRecord>
        {
            Make(1, speed: 90, accuracy: 98),
            Make(2, speed: 110, accuracy: 98)
        };

        var best = BestScoreSelector.SelectBest(records);
        Assert.NotNull(best);
        Assert.Equal(2, best!.AttemptIndex);
        Assert.Equal(110, best.Speed);
    }

    [Fact]
    public void FormatBest_IncludesAttemptAndScores()
    {
        var text = BestScoreSelector.FormatBest(Make(3, 88.5, 97.2));
        Assert.Contains("第3次", text);
        Assert.Contains("88.5", text);
        Assert.Contains("97.2", text);
    }

    [Fact]
    public void SelectBest_Empty_ReturnsNull()
    {
        Assert.Null(BestScoreSelector.SelectBest(Array.Empty<ExamRecord>()));
        Assert.Equal(string.Empty, BestScoreSelector.FormatBest(null));
    }

    private static ExamRecord Make(int attempt, double speed, double accuracy) => new()
    {
        RecordId = Guid.NewGuid(),
        SessionId = Guid.NewGuid(),
        StudentId = "S001",
        AttemptIndex = attempt,
        Speed = speed,
        Accuracy = accuracy,
        SubmittedAt = DateTime.UtcNow
    };
}
