namespace CourtBookingManagement.Application.Matching.Models.Responses;

public sealed class MyMatchResponse
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public Guid BranchId { get; set; }

    public string BranchName { get; set; } = string.Empty;

    public string? CourtName { get; set; }

    public DateOnly MatchDate { get; set; }

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }

    public string SkillLevel { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public int MaxPlayers { get; set; }

    public int CurrentPlayers { get; set; }

    public int RemainingSlots { get; set; }

    public decimal? FeePerPlayer { get; set; }

    public bool IsHost { get; set; }

    public bool Joined { get; set; }

    public bool PendingApproval { get; set; }

    public DateTime CreatedAt { get; set; }
}