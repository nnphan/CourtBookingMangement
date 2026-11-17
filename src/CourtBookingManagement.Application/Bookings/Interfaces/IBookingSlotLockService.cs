using CourtBookingManagement.Domain.Bookings;

namespace CourtBookingManagement.Application.Bookings.Interfaces;

public interface IBookingSlotLockService
{
    Task<IBookingSlotLockHandle?> AcquireAsync(
        IEnumerable<BookingSlotResource> resources,
        CancellationToken cancellationToken);
}

public interface IBookingSlotLockHandle : IAsyncDisposable
{
}
