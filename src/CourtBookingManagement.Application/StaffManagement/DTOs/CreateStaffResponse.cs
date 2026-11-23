namespace CourtBookingManagement.Application.StaffManagement.DTOs;

/// <summary>Creation result and the outcome of activation email delivery.</summary>
public sealed record CreateStaffResponse(
    Guid UserId,
    string InvitationDeliveryStatus,
    string? WarningCode);
