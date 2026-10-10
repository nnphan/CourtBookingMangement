using CourtBookingManagement.Application.Admin.Branches.DTOs;
using CourtBookingManagement.Application.Admin.Branches.Interfaces;
using CourtBookingManagement.Application.Matching.Models;

namespace CourtBookingManagement.Application.Admin.Branches.Services;

public sealed class AdminBranchService(IAdminBranchRepository repository) : IAdminBranchService
{
    public Task<BranchSummaryResponse> GetSummaryAsync(CancellationToken cancellationToken) =>
        repository.GetSummaryAsync(cancellationToken);

    public Task<PagedResult<BranchListResponse>> GetBranchesAsync(
        GetBranchesRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        request.Keyword = request.Keyword?.Trim();
        request.City = request.City?.Trim();
        request.District = request.District?.Trim();

        return repository.GetBranchesAsync(request, cancellationToken);
    }
}