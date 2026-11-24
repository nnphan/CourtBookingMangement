using CourtBookingManagement.Application.Matching.Models;
using CourtBookingManagement.Application.StaffManagement.DTOs;
using CourtBookingManagement.Application.StaffManagement.Models;

namespace CourtBookingManagement.Application.StaffManagement.Interfaces;

public interface IBranchOwnerStaffRepository
{
    Task<StaffOwnerScope?> GetOwnerScopeAsync(Guid ownerUserId, CancellationToken cancellationToken);

    Task<PagedResult<StaffListItemResponse>> SearchAsync(
        Guid ownerId,
        IReadOnlyCollection<Guid> branchIds,
        StaffListRequest request,
        CancellationToken cancellationToken);

    Task<StaffDetailResponse?> GetByIdAsync(
        Guid ownerId,
        Guid userId,
        IReadOnlyCollection<Guid> branchIds,
        CancellationToken cancellationToken);

    Task<StaffCreationOutcome> CreateAsync(StaffCreation creation, CancellationToken cancellationToken);

    Task<bool> SetInvitationDeliveryAsync(
        Guid invitationId, Guid actorId, bool sent, CancellationToken cancellationToken);

    Task<StaffInvitationIssueResult> IssueInvitationAsync(
        Guid ownerId,
        Guid actorId,
        Guid userId,
        Guid invitationId,
        string tokenHash,
        DateTimeOffset expiresAt,
        int resendCooldownMinutes,
        int maxResendsPerWindow,
        int resendWindowMinutes,
        CancellationToken cancellationToken);

    Task<bool> ActivateAsync(
        string tokenHash,
        string passwordHash,
        short linkVersion,
        string? normalizedEmail,
        CancellationToken cancellationToken);
}
