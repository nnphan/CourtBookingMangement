namespace CourtBookingManagement.Application.StaffManagement.DTOs;

/// <summary>Activates a Staff invitation and sets the account password.</summary>
public sealed record ActivateStaffInvitationRequest
{
    /// <summary>The one-time invitation token from the activation link.</summary>
    public string Token { get; init; } = string.Empty;

    /// <summary>The email address the invitation was sent to.</summary>
    public string Email { get; init; } = string.Empty;

    /// <summary>The password to set for the Staff account.</summary>
    public string Password { get; init; } = string.Empty;

    /// <summary>Confirmation of the password to set.</summary>
    public string ConfirmPassword { get; init; } = string.Empty;
}
