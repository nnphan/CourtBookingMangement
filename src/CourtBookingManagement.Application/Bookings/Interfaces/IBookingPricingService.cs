using CourtBookingManagement.Application.Bookings.DTOs;

namespace CourtBookingManagement.Application.Bookings.Interfaces;

public interface IBookingPricingService
{
    Task<IReadOnlyList<decimal>> CalculatePricesAsync(
        Guid branchId,
        IReadOnlyList<BookingPricingRequestDetail> details,
        CancellationToken cancellationToken);

    Task<decimal> CalculatePriceAsync(
        Guid branchId,
        DateOnly bookingDate,
        TimeOnly startTime,
        TimeOnly endTime,
        CancellationToken cancellationToken);
}