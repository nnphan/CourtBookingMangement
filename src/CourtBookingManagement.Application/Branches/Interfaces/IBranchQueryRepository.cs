using CourtBookingManagement.Application.Branches.DTOs;
using CourtBookingManagement.Application.Branches.DTOs.Admin;
using CourtBookingManagement.Application.Branches.DTOs.Requests;

namespace CourtBookingManagement.Application.Branches.Interfaces;

public interface IBranchQueryRepository
{
    Task<BranchAdminSearchResult> SearchBranchesAsync(
        BranchAdminSearchRequest request,
        CancellationToken cancellationToken);

    Task<BranchAdminResponse?> GetBranchDetailsAsync(
        Guid branchId,
        CancellationToken cancellationToken);
}