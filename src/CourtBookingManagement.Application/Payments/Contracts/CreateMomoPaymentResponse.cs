namespace CourtBookingManagement.Application.Payments.Contracts;

public sealed record CreateMomoPaymentResponse(
    Guid PaymentId,
    Guid BookingId,
    string Status,
    decimal Amount,
    string? PayUrl,
    string? Deeplink,
    string? QrCodeUrl);
