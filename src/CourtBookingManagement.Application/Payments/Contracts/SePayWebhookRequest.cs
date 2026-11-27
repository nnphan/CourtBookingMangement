using System.Text.Json.Serialization;

namespace CourtBookingManagement.Application.Payments.Contracts;

public sealed class SePayWebhookRequest
{
    [JsonIgnore]
    public Guid? ApiTransactionId { get; init; }

    [JsonIgnore]
    public string TransactionIdentity => ApiTransactionId?.ToString("D")
        ?? Id.ToString(System.Globalization.CultureInfo.InvariantCulture);

    [JsonPropertyName("id")]
    [JsonRequired]
    public long Id { get; init; }

    [JsonPropertyName("gateway")]
    public string? Gateway { get; init; }

    [JsonPropertyName("transactionDate")]
    public string? TransactionDate { get; init; }

    [JsonPropertyName("accountNumber")]
    public string? AccountNumber { get; init; }

    [JsonPropertyName("subAccount")]
    public string? SubAccount { get; init; }

    [JsonPropertyName("code")]
    public string? Code { get; init; }

    [JsonPropertyName("content")]
    public string? Content { get; init; }

    [JsonPropertyName("transferType")]
    public string? TransferType { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("transferAmount")]
    public decimal TransferAmount { get; init; }

    [JsonPropertyName("accumulated")]
    public decimal Accumulated { get; init; }

    [JsonPropertyName("referenceCode")]
    public string? ReferenceCode { get; init; }
}
