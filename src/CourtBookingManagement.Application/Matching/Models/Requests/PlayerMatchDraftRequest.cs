namespace CourtBookingManagement.Application.Matching.Models.Requests;

public sealed class PlayerMatchDraftRequest
{
    public Guid? BookingId { get; set; }

    public Guid BranchId { get; set; }

    public Guid? CourtId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DateOnly? MatchDate { get; set; }

    public TimeOnly? StartTime { get; set; }

    public TimeOnly? EndTime { get; set; }

    public string GameType { get; set; } = string.Empty;

    public int? MinimumRating { get; set; }

    public int? MaximumRating { get; set; }

    public int RequiredPlayerCount { get; set; }

    public string GenderPolicy { get; set; } = string.Empty;

    public string? RequiredGender { get; set; }

    public decimal? ParticipationFee { get; set; }
}
