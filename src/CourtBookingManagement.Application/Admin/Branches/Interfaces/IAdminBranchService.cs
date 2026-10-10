using CourtBookingManagement.Application.Admin.Branches.DTOs;
using CourtBookingManagement.Application.Matching.Models;

namespace CourtBookingManagement.Application.Admin.Branches.Interfaces;

public interface IAdminBranchService
{
    Task<BranchSummaryResponse> GetSummaryAsync(CancellationToken cancellationToken);

    Task<PagedResult<BranchListResponse>> GetBranchesAsync(
        GetBranchesRequest request,
        CancellationToken cancellationToken);
}