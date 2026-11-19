using CourtBookingManagement.Application.Matching.Models;

namespace CourtBookingManagement.Application.Matching.Models.Requests;

public sealed class UpdatePlayerMatchPreferencesRequest
{
    public required PlayerMatchPreferenceWeights Preferences { get; set; }

    public required IReadOnlyList<Guid> BranchIds { get; set; } = [];

    public TimeOnly? PreferredStartTime { get; set; }

    public TimeOnly? PreferredEndTime { get; set; }

    public int MinimumTimeOverlapMinutes { get; set; } = 60;

    public decimal? MaximumParticipationFee { get; set; }
}
