namespace CourtBookingManagement.Application.StaffManagement.Models;

public sealed record StaffInvitationState
{
    public Guid InvitationId { get; init; }

    public Guid UserId { get; init; }

    public string Email { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public DateTime ExpiresAt { get; init; }

    public DateTime DatabaseTime { get; init; }

    public DateTime? UsedAt { get; init; }

    public DateTime? RevokedAt { get; init; }

    public short LinkVersion { get; init; }

    public string? IssuanceType { get; init; }

    public bool UserIsActive { get; init; }

    public bool UserIsEmailVerified { get; init; }

    public bool HasActiveStaffAssignment { get; init; }

    public bool HasNonStaffRole { get; init; }

    public string[] BranchNames { get; init; } = [];

    public bool IsExpired => ExpiresAt <= DatabaseTime;
}
