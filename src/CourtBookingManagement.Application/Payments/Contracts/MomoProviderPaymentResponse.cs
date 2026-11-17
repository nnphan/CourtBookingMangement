namespace CourtBookingManagement.Application.Payments.Contracts;

public sealed class MomoProviderPaymentResponse
{
    public string PartnerCode { get; init; } = string.Empty;
    public string OrderId { get; init; } = string.Empty;
    public string RequestId { get; init; } = string.Empty;
    public long Amount { get; init; }
    public int ResultCode { get; init; }
    public string Message { get; init; } = string.Empty;
    public string? PayUrl { get; init; }
    public string? Deeplink { get; init; }
    public string? QrCodeUrl { get; init; }
    public string ExtraData { get; init; } = string.Empty;
    public string OrderInfo { get; init; } = string.Empty;
    public string OrderType { get; init; } = string.Empty;
    public string PayType { get; init; } = string.Empty;
    public long ResponseTime { get; init; }
    public long TransactionId { get; init; }
    public string? Signature { get; init; }
}
