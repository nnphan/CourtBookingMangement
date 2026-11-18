namespace CourtBookingManagement.Application.Payments.Contracts;

public sealed class SePayBookingPaymentContext
{
    public Guid BookingId { get; init; }
    public string BookingCode { get; init; } = string.Empty;
    public string BookingStatus { get; init; } = string.Empty;
    public string PaymentMethod { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }
    public DateTimeOffset ServerTime { get; init; }
}
