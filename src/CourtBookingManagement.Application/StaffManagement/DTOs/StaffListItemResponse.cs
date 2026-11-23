namespace CourtBookingManagement.Application.StaffManagement.DTOs;

/// <summary>Represents Staff information returned by the owner-scoped list endpoint.</summary>
public sealed record StaffListItemResponse(
    Guid UserId,
    string DisplayName,
    string Email,
    string? PhoneNumber,
    string? StaffCode,
    string Status,
    string ActivationStatus,
    IReadOnlyList<StaffBranchResponse> Branches,
    DateTimeOffset? LastLoginAt,
    DateTimeOffset CreatedAt);
