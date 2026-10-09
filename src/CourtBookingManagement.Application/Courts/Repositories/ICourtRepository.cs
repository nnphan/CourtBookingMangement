using CourtBookingManagement.Application.Courts.Models.Requests;
using CourtBookingManagement.Application.Courts.Models.Responses;
using CourtBookingManagement.Application.Matching.Models;

namespace CourtBookingManagement.Application.Courts.Repositories;

public interface ICourtRepository
{
    Task<PagedResult<CourtListItemResponse>> SearchAsync(
        CourtSearchRequest request,
        CancellationToken cancellationToken);

    Task<long> CountAsync(
        CourtSearchRequest request,
        CancellationToken cancellationToken);

    Task<CourtDetailResponse?> GetByIdAsync(
        Guid courtId,
        CancellationToken cancellationToken);

    Task<bool> ExistsAsync(
        Guid courtId,
        CancellationToken cancellationToken);

    Task<bool> BranchExistsAsync(
        Guid branchId,
        CancellationToken cancellationToken);

    Task<bool> CourtTypeExistsAsync(
        Guid courtTypeId,
        CancellationToken cancellationToken);

    Task<bool> NameExistsAsync(
        Guid branchId,
        string name,
        Guid? excludeCourtId,
        CancellationToken cancellationToken);

    Task<bool> CourtNumberExistsAsync(
        Guid branchId,
        int courtNumber,
        Guid? excludeCourtId,
        CancellationToken cancellationToken);

    Task<bool> HasFutureBookingsAsync(
        Guid courtId,
        CancellationToken cancellationToken);

    Task<bool> HasFutureMatchesAsync(
        Guid courtId,
        CancellationToken cancellationToken);

    Task<Guid> CreateAsync(
        CreateCourtRequestDTO request,
        Guid createdBy,
        CancellationToken cancellationToken);

    Task<bool> UpdateAsync(
        Guid courtId,
        UpdateCourtRequest request,
        Guid updatedBy,
        CancellationToken cancellationToken);

    Task<bool> DeleteAsync(
        Guid courtId,
        Guid deletedBy,
        CancellationToken cancellationToken);
}
