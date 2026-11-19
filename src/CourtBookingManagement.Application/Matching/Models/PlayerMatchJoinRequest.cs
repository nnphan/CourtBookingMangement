namespace CourtBookingManagement.Application.Matching.Models;

public sealed class PlayerMatchJoinRequest
{
    public Guid Id { get; set; }

    public Guid MatchId { get; set; }

    public Guid UserId { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTime? ReviewedAt { get; set; }

    public Guid? ReviewedBy { get; set; }

    public string? Message { get; set; }

    public string? RejectionReason { get; set; }
}