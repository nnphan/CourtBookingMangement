using CourtBookingManagement.Application.Matching.Models;
using CourtBookingManagement.Application.Owners.Models;

namespace CourtBookingManagement.Application.Owners.Repositories;

public enum OwnerWriteStatus
{
    Success,
    NotFound,
    UserNotFound,
    UserAlreadyOwner,
    TaxIdTaken,
    HasActiveBranches
}

public interface IOwnerRepository
{
    Task<PagedResult<OwnerResponse>> SearchAsync(OwnerSearchRequest request, CancellationToken cancellationToken);

    Task<OwnerResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<(OwnerWriteStatus Status, Guid Id)> CreateAsync(
        CreateOwnerRequest request,
        Guid? actorId,
        CancellationToken cancellationToken);

    Task<OwnerWriteStatus> UpdateAsync(
        Guid id,
        UpdateOwnerRequest request,
        Guid? actorId,
        CancellationToken cancellationToken);

    Task<OwnerWriteStatus> DeleteAsync(Guid id, Guid? actorId, CancellationToken cancellationToken);
}
