using CourtBookingManagement.Application.StaffManagement.DTOs;

namespace CourtBookingManagement.Application.StaffManagement.Models;

public sealed record StaffCreation(
    Guid UserId,
    Guid InvitationId,
    Guid OwnerId,
    Guid ActorId,
    CreateStaffRequest Request,
    string PasswordHash,
    string TokenHash,
    DateTimeOffset ExpiresAt);

public enum StaffCreationOutcome
{
    Created,
    BranchAccessDenied,
    StaffRoleUnavailable,
    UserAlreadyExists,
    StaffCodeAlreadyUsed
}
