namespace CourtBookingManagement.Application.Matching.Models;

public sealed class PlayerMatchLifecycleContext
{
    public Guid Id { get; set; }

    public Guid CreatedBy { get; set; }

    public Guid BranchId { get; set; }

    public Guid? CourtId { get; set; }

    public Guid? BookingId { get; set; }

    public DateOnly? MatchDate { get; set; }

    public TimeOnly? StartTime { get; set; }

    public TimeOnly? EndTime { get; set; }

    public int MaximumPlayers { get; set; }

    public int CurrentPlayers { get; set; }

    public string? GameType { get; set; }

    public int? MinimumRating { get; set; }

    public int? MaximumRating { get; set; }

    public string? GenderPolicy { get; set; }

    public string? RequiredGender { get; set; }

    public decimal? ParticipationFee { get; set; }

    public string Status { get; set; } = string.Empty;
}
