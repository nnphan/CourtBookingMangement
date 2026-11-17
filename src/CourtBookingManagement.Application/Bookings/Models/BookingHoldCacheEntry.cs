namespace CourtBookingManagement.Application.Bookings.Models;

public sealed record BookingHoldCacheEntry(
    Guid BookingId,
    string BookingCode,
    string Status,
    DateTimeOffset ExpiresAt,
    IReadOnlyList<Guid> CourtIds);
