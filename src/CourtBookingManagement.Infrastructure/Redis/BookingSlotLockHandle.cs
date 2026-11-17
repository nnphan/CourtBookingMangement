using CourtBookingManagement.Application.Bookings.Interfaces;

namespace CourtBookingManagement.Infrastructure.Redis;

internal sealed class BookingSlotLockHandle(
    IReadOnlyList<IAsyncDisposable> locks) : IBookingSlotLockHandle
{
    private IReadOnlyList<IAsyncDisposable>? _locks = locks;

    public async ValueTask DisposeAsync()
    {
        var locksToDispose = Interlocked.Exchange(ref _locks, null);
        if (locksToDispose is null)
        {
            return;
        }

        for (var index = locksToDispose.Count - 1; index >= 0; index--)
        {
            await locksToDispose[index].DisposeAsync();
        }
    }
}
