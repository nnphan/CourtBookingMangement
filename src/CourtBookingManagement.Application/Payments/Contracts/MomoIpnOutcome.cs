using CourtBookingManagement.Domain.Bookings;

namespace CourtBookingManagement.Application.Payments.Contracts;

public sealed record MomoIpnOutcome(
    MomoIpnResponse Response,
    Guid? BookingId,
    IReadOnlyList<BookingSlotResource> Resources);
