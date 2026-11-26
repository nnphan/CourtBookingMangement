using CourtBookingManagement.Application.Matching.Models;

namespace CourtBookingManagement.Application.Matching.Models.Responses;

public sealed class PersonalizedPlayerMatchSearchResponse
{
    public IReadOnlyList<PersonalizedPlayerMatchResponse> Items { get; set; } = [];

    public PlayerMatchPreferenceWeights AppliedWeights { get; set; } = new()
    {
        SkillWeight = 40,
        TimeWeight = 30,
        BranchWeight = 20,
        GenderWeight = 10
    };

    public bool CandidateLimitReached { get; set; }
}
