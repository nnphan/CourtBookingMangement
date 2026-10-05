using CourtBookingManagement.Application.Branches.DTOs;

namespace CourtBookingManagement.Application.Branches.Interfaces;

public interface IBranchQueryRepository
{
    Task<BranchDetailsResponse?> GetBranchDetailsAsync(
        Guid branchId,
        CancellationToken cancellationToken);
}