namespace CourtBookingManagement.Application.Matching.Models.Responses;

public sealed class PlayerMatchingProfileResponse
{
    public int CurrentRating { get; set; }

    public string RatingBand { get; set; } = string.Empty;

    public decimal RatingConfidence { get; set; }

    public string RatingSource { get; set; } = string.Empty;

    public int MatchesPlayed { get; set; }

    public string? Gender { get; set; }

    public bool IsProfilePublic { get; set; }

    public bool IsMatchingEnabled { get; set; }
}
