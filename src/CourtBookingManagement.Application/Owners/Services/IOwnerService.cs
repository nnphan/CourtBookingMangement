using CourtBookingManagement.Application.Matching.Models;
using CourtBookingManagement.Application.Owners.Models;
using CourtBookingManagement.Domain.Abstractions;

namespace CourtBookingManagement.Application.Owners.Services;

public interface IOwnerService
{
    Task<Result<PagedResult<OwnerResponse>>> SearchAsync(OwnerSearchRequest request, CancellationToken cancellationToken);

    Task<Result<OwnerResponse>> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<Result<OwnerResponse>> CreateAsync(CreateOwnerRequest request, Guid? actorId, CancellationToken cancellationToken);

    Task<Result<OwnerResponse>> UpdateAsync(Guid id, UpdateOwnerRequest request, Guid? actorId, CancellationToken cancellationToken);

    Task<Result> DeleteAsync(Guid id, Guid? actorId, CancellationToken cancellationToken);
}
