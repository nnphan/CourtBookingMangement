using CourtBookingManagement.Application.Common.Constants;

namespace CourtBookingManagement.Application.Matching.Models.Requests;

public sealed class CreatePlayerMatchRequest
{
    public Guid BookingId { get; set; }

    public Guid BranchId { get; set; }

    public Guid? CourtId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DateOnly MatchDate { get; set; }

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }

    /// <summary>
    /// Allowed values: BEGINNER, INTERMEDIATE, ADVANCED, PROFESSIONAL.
    /// </summary>
    public string SkillLevel { get; set; } = string.Empty;

    /// <summary>
    /// Allowed values: MALE, FEMALE, BOTH. Defaults to BOTH.
    /// </summary>
    public string GenderPreference { get; set; } = GenderPreferences.Both;

    public int MaxPlayers { get; set; }

    public int CurrentPlayers { get; set; } = 1;

    public decimal? FeePerPlayer { get; set; }
}
