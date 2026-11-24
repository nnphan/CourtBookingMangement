namespace CourtBookingManagement.Application.StaffManagement.DTOs;

/// <summary>Safe invitation information displayed before Staff activation.</summary>
public sealed record StaffInvitationValidationResponse(
    bool Valid,
    string DisplayName,
    IReadOnlyList<string> BranchNames,
    DateTimeOffset ExpiresAt);
