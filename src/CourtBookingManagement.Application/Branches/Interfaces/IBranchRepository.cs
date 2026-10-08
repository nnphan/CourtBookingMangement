using System.Data;
using CourtBookingManagement.Application.Branches.DTOs;

namespace CourtBookingManagement.Application.Branches.Interfaces;

public interface IBranchRepository
{
    Task<Guid?> GetOwnerIdForUserAsync(Guid userId, CancellationToken cancellationToken);

    Task<bool> ExistsAsync(Guid ownerId, string name, CancellationToken cancellationToken);

    Task<bool> ExistsByIdAsync(Guid branchId, CancellationToken cancellationToken);

    Task<Guid> AddAsync(CreateBranchData branch, CancellationToken cancellationToken);

    Task<bool> UpdateAsync(
        Guid branchId,
        UpdateBranchData branch,
        Guid updatedBy,
        CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);

    // Returns null when the branch does not exist, otherwise whether it has been soft deleted.
    Task<bool?> IsDeletedAsync(Guid branchId, CancellationToken cancellationToken);

    // Returns false when the branch was soft deleted concurrently and nothing was updated.
    Task<bool> SoftDeleteAsync(
        Guid branchId,
        Guid deletedBy,
        IDbTransaction transaction,
        CancellationToken cancellationToken);
}
