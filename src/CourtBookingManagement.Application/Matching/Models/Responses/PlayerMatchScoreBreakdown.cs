namespace CourtBookingManagement.Application.Matching.Models.Responses;

public sealed class PlayerMatchScoreBreakdown
{
    public decimal SkillScore { get; set; }

    public decimal TimeScore { get; set; }

    public decimal BranchScore { get; set; }

    public decimal GenderScore { get; set; }

    public decimal SkillContribution { get; set; }

    public decimal TimeContribution { get; set; }

    public decimal BranchContribution { get; set; }

    public decimal GenderContribution { get; set; }
}
