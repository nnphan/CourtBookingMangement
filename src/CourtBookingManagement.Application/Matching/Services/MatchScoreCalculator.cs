using CourtBookingManagement.Application.Common.Constants;
using CourtBookingManagement.Application.Matching.Models;

namespace CourtBookingManagement.Application.Matching.Services;

public static class MatchScoreCalculator
{
    public static decimal CalculateSkillScore(string? userLevel, string? roomLevel)
    {
        var userIndex = Array.IndexOf(SkillLevels.All, Normalize(userLevel));
        var roomIndex = Array.IndexOf(SkillLevels.All, Normalize(roomLevel));
        if (userIndex < 0 || roomIndex < 0)
        {
            return 0m;
        }

        return Math.Abs(userIndex - roomIndex) switch
        {
            0 => 100m,
            1 => 70m,
            2 => 40m,
            _ => 10m
        };
    }

    public static decimal CalculateTimeScore(TimeOnly? searchStart, TimeOnly? searchEnd, TimeOnly roomStart, TimeOnly roomEnd)
    {
        if (searchStart is not TimeOnly start || searchEnd is not TimeOnly end
            || start >= end || roomStart >= roomEnd)
        {
            return 0m;
        }

        var overlapStart = Math.Max(start.Ticks, roomStart.Ticks);
        var overlapEnd = Math.Min(end.Ticks, roomEnd.Ticks);
        if (overlapEnd <= overlapStart)
        {
            return 0m;
        }

        var unionStart = Math.Min(start.Ticks, roomStart.Ticks);
        var unionEnd = Math.Max(end.Ticks, roomEnd.Ticks);
        return (overlapEnd - overlapStart) * 100m / (unionEnd - unionStart);
    }

    public static decimal CalculateBranchScore(Guid? searchBranchId, Guid roomBranchId) =>
        searchBranchId == roomBranchId && roomBranchId != Guid.Empty ? 100m : 0m;

    public static decimal CalculateGenderScore(string? userGender, string? roomGender)
    {
        var preference = Normalize(roomGender);
        if (preference is GenderPreferences.Both or "ANY")
        {
            return 100m;
        }

        return (preference is GenderPreferences.Male or GenderPreferences.Female or "OTHER")
            && preference == Normalize(userGender) ? 100m : 0m;
    }

    public static decimal CalculateMatchScore(
        decimal skillScore,
        decimal timeScore,
        decimal branchScore,
        decimal genderScore,
        PlayerMatchPreferenceWeights weights)
    {
        if (!AreWeightsValid(weights))
        {
            throw new ArgumentException("Weights must be between 0 and 100 and sum to 100.", nameof(weights));
        }

        return Math.Clamp(
            CalculateContribution(skillScore, weights.SkillWeight)
            + CalculateContribution(timeScore, weights.TimeWeight)
            + CalculateContribution(branchScore, weights.BranchWeight)
            + CalculateContribution(genderScore, weights.GenderWeight),
            0m,
            100m);
    }

    public static decimal GetWeightTotal(PlayerMatchPreferenceWeights weights) =>
        weights.SkillWeight + weights.TimeWeight + weights.BranchWeight + weights.GenderWeight;

    public static bool AreWeightsValid(PlayerMatchPreferenceWeights weights) =>
        weights.SkillWeight is >= 0m and <= 100m
        && weights.TimeWeight is >= 0m and <= 100m
        && weights.BranchWeight is >= 0m and <= 100m
        && weights.GenderWeight is >= 0m and <= 100m
        && GetWeightTotal(weights) == 100m;

    public static decimal CalculateContribution(decimal score, decimal weight) =>
        score * (weight / 100m);

    private static string? Normalize(string? value) => value?.Trim().ToUpperInvariant();
}
