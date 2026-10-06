namespace CourtBookingManagement.Application.Matching.Models.Responses;

public sealed class ApproveJoinRequestResponse
{
    public Guid MatchId { get; set; }

    public Guid RequestId { get; set; }

    public Guid UserId { get; set; }

    public string Status { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;
}