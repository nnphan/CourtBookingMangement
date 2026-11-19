namespace CourtBookingManagement.Application.Matching.Models;

public sealed class PlayerMatchPreferenceWeights
{
    public required decimal SkillWeight { get; set; }

    public required decimal TimeWeight { get; set; }

    public required decimal BranchWeight { get; set; }

    public required decimal GenderWeight { get; set; }
}
