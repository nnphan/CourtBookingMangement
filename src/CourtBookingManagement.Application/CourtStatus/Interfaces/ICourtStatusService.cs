using CourtBookingManagement.Application.CourtStatus.DTOs;
using CourtBookingManagement.Domain.Abstractions;

namespace CourtBookingManagement.Application.CourtStatus.Interfaces;

public interface ICourtStatusService
{
    Task<Result<IReadOnlyList<BranchDto>>> GetBranchesAsync(CancellationToken cancellationToken);
    Task<Result<CourtStatusResponse>> GetBoardAsync(Guid branchId, DateOnly date, CancellationToken cancellationToken);
    Task<Result<BookingDetailResponse>> GetBookingAsync(Guid id, CancellationToken cancellationToken);
    Task<Result<BookingDetailResponse>> CreateBookingAsync(CreateBookingRequest request, CancellationToken cancellationToken);
    Task<Result<BookingDetailResponse>> UpdateBookingAsync(Guid id, UpdateBookingRequest request, CancellationToken cancellationToken);
    Task<Result> CancelBookingAsync(Guid id, CancellationToken cancellationToken);
}