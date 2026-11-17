using CourtBookingManagement.Application.Bookings.Models;

namespace CourtBookingManagement.Application.Bookings.Interfaces;

public interface IBookingExpirationRepository
{
    Task<IReadOnlyList<ExpiredBookingHold>> ExpireBatchAsync(
        int batchSize,
        CancellationToken cancellationToken);
}
