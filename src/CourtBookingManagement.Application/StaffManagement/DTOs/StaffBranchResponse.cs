namespace CourtBookingManagement.Application.StaffManagement.DTOs;

/// <summary>Represents one active Staff assignment in an owned branch.</summary>
public sealed record StaffBranchResponse(
    Guid BranchId,
    string BranchName,
    string AssignmentStatus,
    string? StaffCode,
    string? JobTitle,
    DateOnly? EmploymentStartDate,
    string? Notes);
