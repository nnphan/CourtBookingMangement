using CourtBookingManagement.Application.Matching.Models;

namespace CourtBookingManagement.Application.Matching.Models.Requests;

public class PlayerMatchScoringCriteriaRequest
{
    public IReadOnlyList<Guid> BranchIds { get; set; } = [];

    public string? Keyword { get; set; }

    public string? SkillLevel { get; set; }

    public string? GenderPreference { get; set; }

    public TimeOnly? StartTime { get; set; }

    public TimeOnly? EndTime { get; set; }

    public DateOnly? MatchDate { get; set; }

    public TimeOnly? PreferredStartTime { get; set; }

    public TimeOnly? PreferredEndTime { get; set; }

    public int MinimumTimeOverlapMinutes { get; set; } = 60;

    public IReadOnlyList<string> GameTypes { get; set; } = [];

    public decimal? MaximumParticipationFee { get; set; }

    public PlayerMatchPreferenceWeights? Preferences { get; set; }
}
