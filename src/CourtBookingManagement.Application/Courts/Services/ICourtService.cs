using CourtBookingManagement.Application.Courts.Models.Requests;
using CourtBookingManagement.Application.Courts.Models.Responses;
using CourtBookingManagement.Application.Matching.Models;
using CourtBookingManagement.Domain.Abstractions;

namespace CourtBookingManagement.Application.Courts.Services;

public interface ICourtService
{
    Task<Result<PagedResult<CourtListItemResponse>>> SearchAsync(
        CourtSearchRequest request,
        CancellationToken cancellationToken);

    Task<Result<CourtDetailResponse>> GetByIdAsync(
        Guid courtId,
        CancellationToken cancellationToken);

    Task<Result<CourtDetailResponse>> CreateAsync(
        CreateCourtRequest request,
        Guid createdBy,
        CancellationToken cancellationToken);

    Task<Result<CourtDetailResponse>> UpdateAsync(
        Guid courtId,
        UpdateCourtRequest request,
        Guid updatedBy,
        CancellationToken cancellationToken);

    Task<Result> DeleteAsync(
        Guid courtId,
        Guid deletedBy,
        CancellationToken cancellationToken);
}
