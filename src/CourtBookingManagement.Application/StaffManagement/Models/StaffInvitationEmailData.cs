namespace CourtBookingManagement.Application.StaffManagement.Models;

public sealed record StaffInvitationEmailData(
    string Recipient,
    string Token,
    string DisplayName,
    IReadOnlyList<string> BranchNames,
    DateTimeOffset ExpiresAt);
