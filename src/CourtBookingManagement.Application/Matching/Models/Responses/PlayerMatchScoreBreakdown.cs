namespace CourtBookingManagement.Application.Matching.Models.Responses;

public sealed class PlayerMatchScoreBreakdown
{
    public decimal SkillScore { get; set; }

    public decimal TimeScore { get; set; }

    public decimal BranchScore { get; set; }

    public decimal GenderScore { get; set; }
}
