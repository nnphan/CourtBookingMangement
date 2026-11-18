using CourtBookingManagement.Domain.Bookings;

namespace CourtBookingManagement.Application.Payments.Contracts;

public sealed record SePayWebhookProcessingOutcome(
    bool IsDuplicate,
    Guid? BookingId,
    IReadOnlyList<BookingSlotResource> Resources);
