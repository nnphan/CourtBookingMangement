namespace CourtBookingManagement.Application.StaffManagement.Models;

public sealed record StaffInvitation(
    Guid InvitationId,
    string Email,
    string DisplayName,
    string[] BranchNames,
    bool UserIsActive,
    bool UserIsEmailVerified);
