namespace CourtBookingManagement.Application.StaffManagement.DTOs;

/// <summary>One-time Staff activation token and a new password.</summary>
public sealed record ActivateStaffRequest
{
    public string Token { get; init; } = string.Empty;
    public string NewPassword { get; init; } = string.Empty;
}
