using System.Text.Json.Serialization;

namespace CourtBookingManagement.Application.Payments.Contracts;

public sealed class MomoIpnRequest
{
    [JsonPropertyName("partnerCode")]
    public string PartnerCode { get; init; } = string.Empty;

    [JsonPropertyName("orderId")]
    public string OrderId { get; init; } = string.Empty;

    [JsonPropertyName("requestId")]
    public string RequestId { get; init; } = string.Empty;

    [JsonPropertyName("amount")]
    public long Amount { get; init; }

    [JsonPropertyName("orderInfo")]
    public string OrderInfo { get; init; } = string.Empty;

    [JsonPropertyName("orderType")]
    public string OrderType { get; init; } = string.Empty;

    [JsonPropertyName("transId")]
    public long TransactionId { get; init; }

    [JsonPropertyName("resultCode")]
    public int ResultCode { get; init; }

    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;

    [JsonPropertyName("payType")]
    public string PayType { get; init; } = string.Empty;

    [JsonPropertyName("responseTime")]
    public long ResponseTime { get; init; }

    [JsonPropertyName("extraData")]
    public string ExtraData { get; init; } = string.Empty;

    [JsonPropertyName("signature")]
    public string Signature { get; init; } = string.Empty;
}
