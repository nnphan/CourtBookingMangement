using CourtBookingManagement.Application.Branches.DTOs.Requests;
using CourtBookingManagement.Application.Branches.DTOs;
using CourtBookingManagement.Application.Branches.DTOs.Admin;
using CourtBookingManagement.Application.Branches.DTOs.Responses;
using CourtBookingManagement.Domain.Abstractions;

namespace CourtBookingManagement.Application.Branches.Services;

public interface IBranchService
{
    Task<BranchAdminSearchResult> SearchAsync(
        BranchAdminSearchRequest request,
        CancellationToken cancellationToken);

    Task<CreateBranchResponse> CreateAsync(
        CreateBranchRequest request,
        Guid ownerId,
        CancellationToken cancellationToken);

    Task<Result> DeleteAsync(
        Guid branchId,
        Guid deletedBy,
        CancellationToken cancellationToken);
}