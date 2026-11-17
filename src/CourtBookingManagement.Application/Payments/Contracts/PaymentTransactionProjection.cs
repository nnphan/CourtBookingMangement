namespace CourtBookingManagement.Application.Payments.Contracts;

public sealed class PaymentTransactionProjection
{
    public Guid PaymentId { get; init; }
    public Guid BookingId { get; init; }
    public string OrderId { get; init; } = string.Empty;
    public string RequestId { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string Status { get; init; } = string.Empty;
    public string? PayUrl { get; init; }
    public string? Deeplink { get; init; }
    public string? QrCodeUrl { get; init; }
    public DateTimeOffset? PaidAt { get; init; }
    public bool NeedsProviderRequest { get; init; }
}
