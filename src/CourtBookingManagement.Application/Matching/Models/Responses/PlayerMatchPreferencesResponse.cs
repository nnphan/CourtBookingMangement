using CourtBookingManagement.Application.Matching.Models;

namespace CourtBookingManagement.Application.Matching.Models.Responses;

public sealed class PlayerMatchPreferencesResponse
{
    public PlayerMatchPreferenceWeights Preferences { get; set; } = new()
    {
        SkillWeight = 40,
        TimeWeight = 30,
        BranchWeight = 20,
        GenderWeight = 10
    };

    public IReadOnlyList<Guid> BranchIds { get; set; } = [];

    public TimeOnly? PreferredStartTime { get; set; }

    public TimeOnly? PreferredEndTime { get; set; }

    public int MinimumTimeOverlapMinutes { get; set; } = 60;

    public decimal? MaximumParticipationFee { get; set; }
}
