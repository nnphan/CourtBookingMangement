namespace CourtBookingManagement.Application.Payments.Contracts;

public sealed record GetPaymentStatusResponse(
    Guid PaymentId,
    Guid BookingId,
    string Status,
    decimal Amount,
    string? PayUrl,
    DateTimeOffset? PaidAt,
    string? BookingStatus = null,
    string? BookingCode = null,
    string? PaymentCode = null,
    string? QrCodeUrl = null,
    string? BankCode = null,
    string? BankAccountNumber = null,
    string? AccountHolderName = null,
    decimal? ReceivedAmount = null,
    DateTimeOffset? ExpiresAt = null,
    DateTimeOffset? ServerTime = null,
    int? RemainingSeconds = null,
    string? Provider = null);
