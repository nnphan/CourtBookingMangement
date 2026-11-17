using CourtBookingManagement.Domain.Bookings;

namespace CourtBookingManagement.Application.Bookings.Models;

public sealed record ExpiredBookingHold(
    Guid BookingId,
    IReadOnlyList<BookingSlotResource> Resources);
