using CourtBookingManagement.Application.Bookings.Exceptions;
using CourtBookingManagement.Application.Bookings.Interfaces;
using Microsoft.Extensions.Logging;

namespace CourtBookingManagement.Application.Bookings.Services;

public sealed class BookingExpirationService(
    IBookingExpirationRepository repository,
    IBookingHoldCache holdCache,
    ILogger<BookingExpirationService> logger)
{
    public async Task ExpireBatchAsync(int batchSize, CancellationToken cancellationToken)
    {
        var expiredBookings = await repository.ExpireBatchAsync(batchSize, cancellationToken);
        foreach (var expiredBooking in expiredBookings)
        {
            try
            {
                await holdCache.RemoveAsync(
                    expiredBooking.BookingId,
                    expiredBooking.Resources,
                    cancellationToken);
            }
            catch (BookingHoldCacheUnavailableException exception)
            {
                logger.LogWarning(
                    exception,
                    "Expired booking {BookingId} is committed in PostgreSQL but its Redis hold keys could not be removed.",
                    expiredBooking.BookingId);
            }
        }
    }
}
