using CourtBookingManagement.Application.Branches.DTOs;

namespace CourtBookingManagement.Application.Branches.Interfaces;

public interface IBranchRepository
{
    Task<Guid?> GetOwnerIdForUserAsync(Guid userId, CancellationToken cancellationToken);

    Task<bool> ExistsAsync(Guid ownerId, string name, CancellationToken cancellationToken);

    Task<Guid> AddAsync(CreateBranchData branch, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}