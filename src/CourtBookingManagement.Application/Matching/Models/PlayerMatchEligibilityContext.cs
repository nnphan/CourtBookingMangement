namespace CourtBookingManagement.Application.Matching.Models;

public sealed class PlayerMatchEligibilityContext
{
    public Guid MatchId { get; init; }

    public Guid HostUserId { get; init; }

    public string Status { get; init; } = string.Empty;

    public Guid? BookingId { get; init; }

    public Guid BranchId { get; init; }

    public Guid? CourtId { get; init; }

    public DateOnly? MatchDate { get; init; }

    public TimeOnly? StartTime { get; init; }

    public TimeOnly? EndTime { get; init; }

    public int CurrentPlayers { get; init; }

    public int MaximumPlayers { get; init; }

    public string? GameType { get; init; }

    public int? MinimumRating { get; init; }

    public int? MaximumRating { get; init; }

    public string? GenderPolicy { get; init; }

    public string? RequiredGender { get; init; }

    public bool HasNotStarted { get; init; }
}
