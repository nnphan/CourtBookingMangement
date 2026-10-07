using CourtBookingManagement.Application.Branches.DTOs.Requests;
using CourtBookingManagement.Application.Branches.DTOs.Responses;
using CourtBookingManagement.Domain.Abstractions;

namespace CourtBookingManagement.Application.Branches.Services;

public interface IBranchService
{
    Task<CreateBranchResponse> CreateAsync(
        CreateBranchRequest request,
        Guid ownerId,
        CancellationToken cancellationToken);

    Task<Result> DeleteAsync(
        Guid branchId,
        Guid deletedBy,
        CancellationToken cancellationToken);
}