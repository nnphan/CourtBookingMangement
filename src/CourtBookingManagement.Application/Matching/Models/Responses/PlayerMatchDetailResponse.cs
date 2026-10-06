namespace CourtBookingManagement.Application.Matching.Models.Responses;

public sealed class PlayerMatchDetailResponse
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public Guid BranchId { get; set; }

    public string BranchName { get; set; } = string.Empty;

    public string City { get; set; } = string.Empty;

    public string District { get; set; } = string.Empty;

    public string? CourtName { get; set; }

    public DateOnly MatchDate { get; set; }

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }

    public string SkillLevel { get; set; } = string.Empty;

    public string? GenderPreference { get; set; }

    public int MaxPlayers { get; set; }

    public int CurrentPlayers { get; set; }

    public int RemainingSlots { get; set; }

    public decimal FeePerPlayer { get; set; }

    public string Status { get; set; } = string.Empty;

    public Guid CreatedBy { get; set; }

    public string CreatedByName { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public int ParticipantCount { get; set; }

    public bool IsFull { get; set; }

    public bool CanJoin { get; set; }

    public IReadOnlyCollection<PlayerMatchParticipantResponse> Participants { get; set; } = Array.Empty<PlayerMatchParticipantResponse>();
}