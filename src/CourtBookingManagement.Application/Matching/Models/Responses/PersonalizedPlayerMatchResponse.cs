namespace CourtBookingManagement.Application.Matching.Models.Responses;

public sealed class PersonalizedPlayerMatchResponse
{
    public Guid RoomId { get; set; }

    public string RoomCode { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public Guid BranchId { get; set; }

    public string BranchName { get; set; } = string.Empty;

    public string CourtName { get; set; } = string.Empty;

    public string CourtReservationStatus { get; set; } = "COURT_RESERVED";

    public DateOnly MatchDate { get; set; }

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }

    public string GameType { get; set; } = string.Empty;

    public int MinimumRating { get; set; }

    public int MaximumRating { get; set; }

    public int CurrentPlayerCount { get; set; }

    public int RequiredPlayerCount { get; set; }

    public int AvailableSlots { get; set; }

    public decimal ParticipationFee { get; set; }

    public decimal MatchScore { get; set; }

    public string MatchLevel { get; set; } = string.Empty;

    public PlayerMatchScoreBreakdown ScoreBreakdown { get; set; } = new();

    public IReadOnlyList<string> Reasons { get; set; } = [];

    public IReadOnlyList<string> Warnings { get; set; } = [];

    public string? JoinRequestStatus { get; set; }
}
