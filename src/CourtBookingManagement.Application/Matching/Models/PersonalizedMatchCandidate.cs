namespace CourtBookingManagement.Application.Matching.Models;

public sealed class PersonalizedMatchCandidate
{
    public Guid RoomId { get; set; }

    public string RoomCode { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public Guid BranchId { get; set; }

    public string BranchName { get; set; } = string.Empty;

    public string CourtName { get; set; } = string.Empty;

    public DateOnly MatchDate { get; set; }

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }

    public string GameType { get; set; } = string.Empty;

    public int MinimumRating { get; set; }

    public int MaximumRating { get; set; }

    public int CurrentPlayerCount { get; set; }

    public int RequiredPlayerCount { get; set; }

    public decimal ParticipationFee { get; set; }

    public int BranchPreferenceOrder { get; set; }

    public DateTime CreatedAt { get; set; }
}
