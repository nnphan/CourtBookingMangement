namespace CourtBookingManagement.Application.StaffManagement.DTOs;

/// <summary>Permitted profile fields and branch assignments for a Staff account.</summary>
public sealed record UpdateStaffRequest
{
    public string DisplayName { get; init; } = string.Empty;
    public string PhoneNumber { get; init; } = string.Empty;
    public string? StaffCode { get; init; }
    public string? JobTitle { get; init; }
    public Guid[] BranchIds { get; init; } = [];
    public DateOnly? EmploymentStartDate { get; init; }
    public string? Notes { get; init; }
}
