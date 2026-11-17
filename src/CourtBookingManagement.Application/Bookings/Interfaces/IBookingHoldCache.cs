using CourtBookingManagement.Application.Bookings.Models;
using CourtBookingManagement.Domain.Bookings;

namespace CourtBookingManagement.Application.Bookings.Interfaces;

public interface IBookingHoldCache
{
    Task SetAsync(
        BookingHoldCacheEntry entry,
        IEnumerable<BookingSlotResource> resources,
        TimeSpan timeToLive,
        CancellationToken cancellationToken);

    Task<BookingHoldCacheEntry?> GetAsync(
        Guid bookingId,
        CancellationToken cancellationToken);

    Task RemoveAsync(
        Guid bookingId,
        IEnumerable<BookingSlotResource> resources,
        CancellationToken cancellationToken);
}
