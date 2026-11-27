using CourtBookingManagement.Application.StaffManagement.DTOs;

namespace CourtBookingManagement.Application.StaffManagement.Models;

public sealed record StaffCreation(
    Guid UserId,
    Guid OwnerId,
    Guid ActorId,
    CreateStaffRequest Request,
    string PasswordHash);

public sealed record StaffCreationResult(StaffCreationOutcome Outcome, string? StaffCode = null);

public enum StaffCreationOutcome
{
    Created,
    BranchAccessDenied,
    StaffRoleUnavailable,
    UserAlreadyExists,
    StaffCodeAlreadyUsed
}
