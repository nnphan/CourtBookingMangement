namespace CourtBookingManagement.Application.Matching.Models.Responses;

public sealed class PlayerMatchParticipantResponse
{
    public Guid UserId { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public DateTime JoinedAt { get; set; }
}