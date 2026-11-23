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

    Task SetInvitationDeliveryAsync(Guid invitationId, bool sent, CancellationToken cancellationToken);

    Task<StaffInvitation?> IssueInvitationAsync(
        Guid ownerId, Guid actorId, Guid userId, Guid invitationId, string tokenHash,
        DateTimeOffset expiresAt, CancellationToken cancellationToken);

    Task<bool> ActivateAsync(string tokenHash, string passwordHash, CancellationToken cancellationToken);
}
