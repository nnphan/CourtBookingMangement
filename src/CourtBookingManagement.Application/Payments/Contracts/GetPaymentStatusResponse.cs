namespace CourtBookingManagement.Application.Payments.Contracts;

public sealed record GetPaymentStatusResponse(
    Guid PaymentId,
    Guid BookingId,
    string Status,
    decimal Amount,
    string? PayUrl,
    DateTimeOffset? PaidAt);
