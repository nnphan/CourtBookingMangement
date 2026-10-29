using CourtBookingManagement.Application.Bookings.DTOs;

namespace CourtBookingManagement.Application.Bookings.Interfaces;

public interface IBookingRepository
{
    Task<IReadOnlyList<BookingForPlayerMatchDto>> GetPlayerMatchBookingAsync(Guid bookingId, Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<BranchPricingDto>> GetActivePricingsAsync(Guid branchId, CancellationToken cancellationToken);
    Task<bool> ExistsBranchAsync(Guid branchId, CancellationToken cancellationToken);
    Task<bool> ExistsCourtAsync(Guid courtId, CancellationToken cancellationToken);
    Task<bool> CourtBelongsToBranchAsync(Guid courtId, Guid branchId, CancellationToken cancellationToken);
    Task<bool> HasBookingOverlapAsync(Guid courtId, DateOnly bookingDate, TimeOnly startTime, TimeOnly endTime, CancellationToken cancellationToken);
    Task<CreateBookingResponse?> CreateBookingAsync(CreateBookingRequest request, IReadOnlyList<decimal> detailPrices, Guid? knownCustomerId, CancellationToken cancellationToken);
    Task<BookingDetailResponse?> GetBookingDetailAsync(Guid bookingId, CancellationToken cancellationToken);
    Task<Guid?> GetCustomerIdByUserIdAsync(Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<MyBookingResponse>> GetMyBookingsAsync(Guid customerId, CancellationToken cancellationToken);
}