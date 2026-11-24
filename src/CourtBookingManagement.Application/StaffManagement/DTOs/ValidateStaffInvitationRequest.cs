namespace CourtBookingManagement.Application.StaffManagement.DTOs;

/// <summary>Validates an email-bound Staff invitation.</summary>
public sealed record ValidateStaffInvitationRequest
{
    /// <summary>The one-time invitation token from the activation link.</summary>
    public string Token { get; init; } = string.Empty;

    /// <summary>The email address the invitation was sent to.</summary>
    public string Email { get; init; } = string.Empty;
}
