namespace CourtBookingManagement.Application.StaffManagement.DTOs;

/// <summary>Information for a new Staff account and its owned-branch assignments.</summary>
public sealed record CreateStaffRequest
{
    public string DisplayName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string PhoneNumber { get; init; } = string.Empty;
    public string? StaffCode { get; init; }
    public string? JobTitle { get; init; }
    public Guid[] BranchIds { get; init; } = [];
    public DateOnly? EmploymentStartDate { get; init; }
    public string? Notes { get; init; }
}
