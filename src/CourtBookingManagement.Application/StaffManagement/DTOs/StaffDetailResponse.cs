namespace CourtBookingManagement.Application.StaffManagement.DTOs;

/// <summary>Represents Staff details and assignments visible to the authenticated owner.</summary>
public sealed record StaffDetailResponse(
    Guid UserId,
    string DisplayName,
    string Email,
    string? PhoneNumber,
    string Status,
    string ActivationStatus,
    string? InvitationDeliveryStatus,
    IReadOnlyList<StaffBranchResponse> Branches,
    DateTimeOffset? LastLoginAt,
    DateTimeOffset CreatedAt);
