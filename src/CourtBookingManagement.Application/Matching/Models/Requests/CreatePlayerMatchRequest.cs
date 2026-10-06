namespace CourtBookingManagement.Application.Matching.Models.Requests;

public sealed class CreatePlayerMatchRequest
{
    public Guid BranchId { get; set; }

    public Guid? CourtId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DateOnly MatchDate { get; set; }

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }

    public SkillLevel SkillLevel { get; set; }

    public string? GenderPreference { get; set; }

    public int MaxPlayers { get; set; }

    public decimal? FeePerPlayer { get; set; }
}
