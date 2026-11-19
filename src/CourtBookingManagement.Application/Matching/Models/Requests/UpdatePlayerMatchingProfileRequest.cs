namespace CourtBookingManagement.Application.Matching.Models.Requests;

public sealed class UpdatePlayerMatchingProfileRequest
{
    public required int CurrentRating { get; set; }

    public string? Gender { get; set; }

    public required bool IsProfilePublic { get; set; }

    public required bool IsMatchingEnabled { get; set; }
}
