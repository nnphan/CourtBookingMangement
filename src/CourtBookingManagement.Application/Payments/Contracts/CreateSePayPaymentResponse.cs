namespace CourtBookingManagement.Application.Payments.Contracts;

public sealed record CreateSePayPaymentResponse(
    Guid PaymentId,
    Guid BookingId,
    string BookingCode,
    string PaymentCode,
    decimal Amount,
    string Status,
    string QrUrl,
    string BankCode,
    string BankAccountNumber,
    string AccountHolderName,
    DateTimeOffset ExpiresAt,
    DateTimeOffset ServerTime,
    int RemainingSeconds);
