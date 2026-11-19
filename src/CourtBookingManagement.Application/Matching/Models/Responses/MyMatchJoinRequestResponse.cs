namespace CourtBookingManagement.Application.Matching.Models.Responses;

public sealed class MyMatchJoinRequestResponse
{
    public Guid RequestId { get; init; }

    public Guid MatchId { get; init; }

    public string MatchTitle { get; init; } = string.Empty;

    public string BranchName { get; init; } = string.Empty;

    public string HostName { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public string? Message { get; init; }

    public string? RejectionReason { get; init; }

    public DateTime RequestedAt { get; init; }

    public DateTime? ReviewedAt { get; init; }
}
