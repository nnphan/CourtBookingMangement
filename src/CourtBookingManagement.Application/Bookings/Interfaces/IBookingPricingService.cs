namespace CourtBookingManagement.Application.Bookings.Interfaces;

public interface IBookingPricingService
{
    Task<decimal> CalculatePriceAsync(
        Guid branchId,
        DateOnly bookingDate,
        TimeOnly startTime,
        TimeOnly endTime,
        CancellationToken cancellationToken);
}