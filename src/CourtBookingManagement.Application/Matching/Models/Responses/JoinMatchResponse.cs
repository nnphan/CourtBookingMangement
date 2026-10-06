namespace CourtBookingManagement.Application.Matching.Models.Responses;

public sealed class JoinMatchResponse
{
    public Guid MatchId { get; set; }

    public Guid JoinRequestId { get; set; }

    public string Status { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;
}