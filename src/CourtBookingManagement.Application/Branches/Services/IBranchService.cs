using CourtBookingManagement.Application.Branches.DTOs.Requests;
using CourtBookingManagement.Application.Branches.DTOs.Responses;

namespace CourtBookingManagement.Application.Branches.Services;

public interface IBranchService
{
    Task<CreateBranchResponse> CreateAsync(
        CreateBranchRequest request,
        Guid ownerId,
        CancellationToken cancellationToken);
}