using CourtBookingManagement.Application.Bookings.DTOs;

namespace CourtBookingManagement.Application.Bookings.Interfaces;

public interface IBookingRepository
{
    Task<IReadOnlyList<BookingForPlayerMatchDto>> GetPlayerMatchBookingAsync(Guid bookingId, Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<BranchPricingDto>> GetActivePricingsAsync(Guid branchId, CancellationToken cancellationToken);
    Task<bool> ExistsBranchAsync(Guid branchId, CancellationToken cancellationToken);
    Task<BookingBranchData?> GetBranchBookingDataAsync(Guid branchId, CancellationToken cancellationToken);
    Task<IReadOnlyList<BookingCourtData>> GetCourtsAsync(IReadOnlyCollection<Guid> courtIds, CancellationToken cancellationToken);
    Task<bool> HasBookingConflictsAsync(IReadOnlyCollection<CreateBookingDetailRequest> details, CancellationToken cancellationToken);
    Task<bool> CustomerExistsAsync(Guid customerId, CancellationToken cancellationToken);
    Task<BookingResponse?> CreateBookingAsync(
        CreateBookingRequest request,
        Guid? customerId,
        string? guestName,
        string? guestPhoneNumber,
        Guid? actorId,
        bool requireCustomerOwnership,
        string bookingType,
        string paymentMethod,
        IReadOnlyList<decimal> detailPrices,
        CancellationToken cancellationToken);
    Task<Guid?> GetCustomerIdByUserIdAsync(Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<MyBookingResponse>> GetMyBookingsAsync(Guid customerId, CancellationToken cancellationToken);
}