namespace CourtBookingManagement.Application.StaffManagement.DTOs;

/// <summary>Optional reason for a Staff access-status change.</summary>
public sealed record ChangeStaffStatusRequest
{
    public string? Reason { get; init; }
}
