namespace CourtBookingManagement.Application.Matching.Models.Responses;

public sealed class PlayerMatchJoinRequestResponse
{
    public Guid RequestId { get; init; }

    public Guid MatchId { get; init; }

    public Guid UserId { get; init; }

    public string? FullName { get; init; }

    public string? SkillLevel { get; init; }

    public string Status { get; init; } = string.Empty;

    public DateTime RequestedAt { get; init; }
}
