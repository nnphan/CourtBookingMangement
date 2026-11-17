using CourtBookingManagement.Domain.Bookings;

namespace CourtBookingManagement.Infrastructure.Redis;

internal static class BookingHoldRedisKeys
{
    public static string Booking(Guid bookingId) => $"booking:hold:{bookingId:N}";

    public static string Slot(BookingSlotResource resource) => resource.ToHoldCacheKey();
}
