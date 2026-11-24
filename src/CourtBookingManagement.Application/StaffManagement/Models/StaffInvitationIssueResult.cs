namespace CourtBookingManagement.Application.StaffManagement.Models;

public enum StaffInvitationIssueOutcome
{
    Issued,
    NotFound,
    AlreadyActive,
    Cooldown,
    RateLimited
}

public sealed record StaffInvitationIssueResult(
    StaffInvitationIssueOutcome Outcome,
    StaffInvitation? Invitation = null,
    int? RetryAfterSeconds = null);
