using CourtBookingManagement.Application.Matching.Models;
using CourtBookingManagement.Application.Matching.Models.Requests;
using CourtBookingManagement.Application.Matching.Services;
using CourtBookingManagement.Application.Matching.Validators;
using Xunit;

namespace CourtBookingManagement.Api.Tests;

public sealed class MatchScoreCalculatorTests
{
    private static readonly PlayerMatchPreferenceWeights DefaultWeights = new()
    {
        SkillWeight = 40m,
        TimeWeight = 30m,
        BranchWeight = 20m,
        GenderWeight = 10m
    };

    [Theory]
    [InlineData("intermediate", " INTERMEDIATE ", 100)]
    [InlineData("BEGINNER", "INTERMEDIATE", 70)]
    [InlineData("ADVANCED", "BEGINNER", 40)]
    [InlineData("PROFESSIONAL", "BEGINNER", 10)]
    [InlineData(null, "BEGINNER", 0)]
    [InlineData("BEGINNER", "unknown", 0)]
    public void CalculatesSkillByLevelDistance(string? userLevel, string? roomLevel, int expected) =>
        Assert.Equal((decimal)expected, MatchScoreCalculator.CalculateSkillScore(userLevel, roomLevel));

    [Theory]
    [InlineData("21:00", "22:00", "21:00", "22:00", 100)]
    [InlineData("21:00", "22:00", "21:00", "23:00", 50)]
    [InlineData("21:00", "22:00", "20:00", "22:00", 50)]
    [InlineData("21:00", "23:00", "21:30", "22:30", 50)]
    [InlineData("21:00", "22:00", "21:30", "22:30", 33.333333)]
    [InlineData("21:00", "22:00", "22:00", "23:00", 0)]
    [InlineData("21:00", "22:00", "19:00", "20:00", 0)]
    [InlineData("21:00", "21:00", "21:00", "22:00", 0)]
    [InlineData("22:00", "21:00", "21:00", "22:00", 0)]
    [InlineData("21:00", "22:00", "22:00", "21:00", 0)]
    [InlineData(null, "22:00", "21:00", "22:00", 0)]
    [InlineData("21:00", null, "21:00", "22:00", 0)]
    public void CalculatesTimeFromIntersectionAndUnion(
        string? searchStart, string? searchEnd, string roomStart, string roomEnd, double expected)
    {
        var actual = MatchScoreCalculator.CalculateTimeScore(
            searchStart is null ? null : TimeOnly.Parse(searchStart),
            searchEnd is null ? null : TimeOnly.Parse(searchEnd),
            TimeOnly.Parse(roomStart), TimeOnly.Parse(roomEnd));

        Assert.Equal((decimal)expected, decimal.Round(actual, 6));
        Assert.InRange(actual, 0m, 100m);
    }

    [Fact]
    public void ASubsecondDifferenceCannotProduceAPerfectTimeScore()
    {
        var start = new TimeOnly(21, 0);
        var end = new TimeOnly(22, 0);
        var actual = MatchScoreCalculator.CalculateTimeScore(start, end, start, end.Add(TimeSpan.FromTicks(1)));

        Assert.InRange(actual, 99.9m, 99.999999999m);
    }

    [Theory]
    [InlineData(null, null, true)]
    [InlineData(21, 22, true)]
    [InlineData(21, null, false)]
    [InlineData(null, 22, false)]
    [InlineData(21, 21, false)]
    [InlineData(22, 21, false)]
    public void ValidatesSearchTimePair(int? startHour, int? endHour, bool expected)
    {
        var result = new PlayerMatchScoringCriteriaRequestValidator().Validate(
            new PlayerMatchScoringCriteriaRequest
            {
                StartTime = startHour is int start ? new TimeOnly(start, 0) : null,
                EndTime = endHour is int end ? new TimeOnly(end, 0) : null
            });

        Assert.Equal(expected, result.IsValid);
    }

    [Fact]
    public void CalculatesBranchById()
    {
        var roomId = Guid.NewGuid();
        Assert.Equal(100m, MatchScoreCalculator.CalculateBranchScore(roomId, roomId));
        Assert.Equal(0m, MatchScoreCalculator.CalculateBranchScore(Guid.NewGuid(), roomId));
        Assert.Equal(0m, MatchScoreCalculator.CalculateBranchScore(null, roomId));
    }

    [Theory]
    [InlineData(null, "BOTH", 100)]
    [InlineData("FEMALE", " any ", 100)]
    [InlineData(" female ", "FEMALE", 100)]
    [InlineData("male", "MALE", 100)]
    [InlineData("MALE", "FEMALE", 0)]
    [InlineData(null, "FEMALE", 0)]
    [InlineData("MALE", null, 0)]
    public void CalculatesGenderCompatibility(string? userGender, string? roomGender, int expected) =>
        Assert.Equal((decimal)expected, MatchScoreCalculator.CalculateGenderScore(userGender, roomGender));

    [Theory]
    [InlineData(100, 80, 100, 100, 94)]
    [InlineData(100, 0, 0, 0, 40)]
    [InlineData(0, 100, 0, 0, 30)]
    [InlineData(0, 0, 100, 0, 20)]
    [InlineData(0, 0, 0, 100, 10)]
    [InlineData(0, 0, 0, 0, 0)]
    [InlineData(100, 100, 100, 100, 100)]
    [InlineData(-10, -10, -10, -10, 0)]
    [InlineData(110, 110, 110, 110, 100)]
    public void CalculatesWeightedScore(
        int skillScore, int timeScore, int branchScore, int genderScore, int expected)
    {
        var actual = MatchScoreCalculator.CalculateMatchScore(
            skillScore, timeScore, branchScore, genderScore, DefaultWeights);

        Assert.Equal((decimal)expected, actual);
    }

    [Fact]
    public void PreservesDecimalWeightsAndContributions()
    {
        var weights = new PlayerMatchPreferenceWeights
        {
            SkillWeight = 12.5m,
            TimeWeight = 87.5m,
            BranchWeight = 0m,
            GenderWeight = 0m
        };

        Assert.Equal(9.125m, MatchScoreCalculator.CalculateContribution(73m, weights.SkillWeight));
        Assert.Equal(66m, MatchScoreCalculator.CalculateMatchScore(73m, 65m, 100m, 100m, weights));
    }

    [Theory]
    [InlineData(40, 30, 20, 9)]
    [InlineData(40, 30, 20, 11)]
    [InlineData(-1, 81, 10, 10)]
    public void RejectsInvalidWeightsBeforeScoring(int skill, int time, int branch, int gender)
    {
        var weights = new PlayerMatchPreferenceWeights
        {
            SkillWeight = skill,
            TimeWeight = time,
            BranchWeight = branch,
            GenderWeight = gender
        };

        Assert.Throws<ArgumentException>(() => MatchScoreCalculator.CalculateMatchScore(100m, 100m, 100m, 100m, weights));
    }
}
