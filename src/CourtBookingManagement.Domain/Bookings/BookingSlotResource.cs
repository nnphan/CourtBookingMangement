using System.Globalization;

namespace CourtBookingManagement.Domain.Bookings;

public readonly record struct BookingSlotResource(
    Guid CourtId,
    DateOnly BookingDate,
    TimeOnly StartTime,
    TimeOnly EndTime)
{
    public string ToLockKey() => string.Create(
        CultureInfo.InvariantCulture,
        $"booking:lock:slot:{CourtId:N}:{BookingDate:yyyy-MM-dd}:{StartTime:HH\\:mm\\:ss}:{EndTime:HH\\:mm\\:ss}");

    public string ToHoldCacheKey() => string.Create(
        CultureInfo.InvariantCulture,
        $"booking:hold:slot:{CourtId:N}:{BookingDate:yyyy-MM-dd}:{StartTime:HH\\:mm\\:ss}:{EndTime:HH\\:mm\\:ss}");
}
