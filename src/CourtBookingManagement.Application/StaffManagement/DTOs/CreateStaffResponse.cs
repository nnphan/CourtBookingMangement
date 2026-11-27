namespace CourtBookingManagement.Application.StaffManagement.DTOs;

/// <summary>New staff account details or the delivery outcome for a legacy invitation.</summary>
public sealed record CreateStaffResponse(
    Guid UserId,
    string? InvitationDeliveryStatus = null,
    string? WarningCode = null,
    string? StaffCode = null);
