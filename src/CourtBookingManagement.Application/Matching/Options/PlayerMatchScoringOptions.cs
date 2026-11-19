namespace CourtBookingManagement.Application.Matching.Options;

public sealed class PlayerMatchScoringOptions
{
    public const string SectionName = "PlayerMatching:Scoring";

    public decimal MinimumSkillTolerance { get; set; } = 100m;

    public decimal MinimumMatchScore { get; set; } = 60m;

    public decimal MinimumSkillScore { get; set; } = 40m;

    public decimal MinimumTimeScore { get; set; } = 40m;
}
