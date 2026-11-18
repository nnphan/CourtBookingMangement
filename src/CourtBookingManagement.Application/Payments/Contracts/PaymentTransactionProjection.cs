namespace CourtBookingManagement.Application.Payments.Contracts;

public sealed class PaymentTransactionProjection
{
    public Guid PaymentId { get; init; }
    public Guid BookingId { get; init; }
    public string Provider { get; init; } = string.Empty;
    public string? BookingCode { get; init; }
    public string? BookingStatus { get; init; }
    public string OrderId { get; init; } = string.Empty;
    public string RequestId { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public decimal? ReceivedAmount { get; init; }
    public string Status { get; init; } = string.Empty;
    public string? PaymentCode { get; init; }
    public string? BankCode { get; init; }
    public string? BankAccountNumber { get; init; }
    public string? PayUrl { get; init; }
    public string? Deeplink { get; init; }
    public string? QrCodeUrl { get; init; }
    public DateTimeOffset? PaidAt { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }
    public DateTimeOffset? ServerTime { get; init; }
    public int? RemainingSeconds { get; init; }
    public bool NeedsProviderRequest { get; init; }
}
