using CourtBookingManagement.Application.Matching.Models;
using CourtBookingManagement.Application.StaffManagement.DTOs;
using CourtBookingManagement.Domain.Abstractions;

namespace CourtBookingManagement.Application.StaffManagement.Interfaces;

public interface IBranchOwnerStaffService
{
    Task<Result<IReadOnlyList<StaffOwnedBranchResponse>>> GetBranchesAsync(
        Guid ownerUserId,
        CancellationToken cancellationToken);

    Task<Result<PagedResult<StaffListItemResponse>>> GetListAsync(
        Guid ownerUserId,
        StaffListRequest request,
        CancellationToken cancellationToken);

    Task<Result<StaffDetailResponse>> GetByIdAsync(
        Guid ownerUserId,
        Guid userId,
        CancellationToken cancellationToken);
}
