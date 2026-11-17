using System.Net.Http.Json;
using System.Text.Json.Serialization;
using CourtBookingManagement.Application.Options;
using CourtBookingManagement.Application.Payments.Contracts;
using CourtBookingManagement.Application.Payments.Interfaces;
using Microsoft.Extensions.Options;

namespace CourtBookingManagement.Infrastructure.Momo;

public sealed class MomoClient(
    HttpClient httpClient,
    IOptions<MomoOptions> options) : IMomoClient
{
    public async Task<MomoProviderPaymentResponse> CreatePaymentAsync(
        MomoCreatePaymentResult request,
        string partnerCode,
        string signature,
        CancellationToken cancellationToken)
    {
        var response = await httpClient.PostAsJsonAsync(
            options.Value.CreateEndpoint,
            new MomoCreatePayload
            {
                PartnerCode = partnerCode,
                RequestId = request.RequestId,
                Amount = request.Amount,
                OrderId = request.OrderId,
                OrderInfo = request.OrderInfo,
                RedirectUrl = request.RedirectUrl,
                IpnUrl = request.IpnUrl,
                RequestType = "captureWallet",
                ExtraData = request.ExtraData,
                AutoCapture = true,
                Lang = "en",
                Signature = signature
            },
            cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<MomoProviderPaymentResponse>(
                   cancellationToken: cancellationToken)
               ?? throw new InvalidDataException("MoMo returned an empty payment response.");
    }

    public async Task<MomoProviderPaymentResponse> QueryPaymentAsync(
        MomoQueryPaymentResult request,
        string partnerCode,
        string signature,
        CancellationToken cancellationToken)
    {
        var response = await httpClient.PostAsJsonAsync(
            options.Value.QueryEndpoint,
            new MomoQueryPayload
            {
                PartnerCode = partnerCode,
                RequestId = request.RequestId,
                OrderId = request.OrderId,
                Lang = "en",
                Signature = signature
            },
            cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<MomoProviderPaymentResponse>(
                   cancellationToken: cancellationToken)
               ?? throw new InvalidDataException("MoMo returned an empty payment query response.");
    }

    private sealed class MomoCreatePayload
    {
        [JsonPropertyName("partnerCode")]
        public string PartnerCode { get; init; } = string.Empty;

        [JsonPropertyName("requestId")]
        public string RequestId { get; init; } = string.Empty;

        [JsonPropertyName("amount")]
        public long Amount { get; init; }

        [JsonPropertyName("orderId")]
        public string OrderId { get; init; } = string.Empty;

        [JsonPropertyName("orderInfo")]
        public string OrderInfo { get; init; } = string.Empty;

        [JsonPropertyName("redirectUrl")]
        public string RedirectUrl { get; init; } = string.Empty;

        [JsonPropertyName("ipnUrl")]
        public string IpnUrl { get; init; } = string.Empty;

        [JsonPropertyName("requestType")]
        public string RequestType { get; init; } = string.Empty;

        [JsonPropertyName("extraData")]
        public string ExtraData { get; init; } = string.Empty;

        [JsonPropertyName("autoCapture")]
        public bool AutoCapture { get; init; }

        [JsonPropertyName("lang")]
        public string Lang { get; init; } = string.Empty;

        [JsonPropertyName("signature")]
        public string Signature { get; init; } = string.Empty;
    }

    private sealed class MomoQueryPayload
    {
        [JsonPropertyName("partnerCode")]
        public string PartnerCode { get; init; } = string.Empty;

        [JsonPropertyName("requestId")]
        public string RequestId { get; init; } = string.Empty;

        [JsonPropertyName("orderId")]
        public string OrderId { get; init; } = string.Empty;

        [JsonPropertyName("lang")]
        public string Lang { get; init; } = string.Empty;

        [JsonPropertyName("signature")]
        public string Signature { get; init; } = string.Empty;
    }
}
