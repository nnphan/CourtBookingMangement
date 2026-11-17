using System.Security.Cryptography;
using System.Text;
using CourtBookingManagement.Application.Options;
using CourtBookingManagement.Application.Payments.Contracts;
using CourtBookingManagement.Application.Payments.Interfaces;
using Microsoft.Extensions.Options;

namespace CourtBookingManagement.Infrastructure.Momo;

public sealed class MomoSignatureService(IOptions<MomoOptions> options) : IMomoSignatureService
{
    private static readonly string[] CreateFieldOrder =
    [
        "accessKey", "amount", "extraData", "ipnUrl", "orderId", "orderInfo",
        "partnerCode", "redirectUrl", "requestId", "requestType"
    ];

    private static readonly string[] ProviderResponseFieldOrder =
    [
        "accessKey", "amount", "extraData", "message", "orderId", "orderInfo",
        "orderType", "partnerCode", "payType", "requestId", "responseTime",
        "resultCode", "transId"
    ];

    private static readonly string[] QueryFieldOrder =
    [
        "accessKey", "orderId", "partnerCode", "requestId"
    ];

    public string CreateSignature(IReadOnlyDictionary<string, string> fields)
    {
        var secretKey = options.Value.SecretKey;
        if (string.IsNullOrWhiteSpace(secretKey))
        {
            throw new InvalidOperationException("The MoMo secret key is not configured.");
        }

        var fieldOrder = ResolveFieldOrder(fields);
        var rawSignature = string.Join("&", fieldOrder.Select(name =>
            $"{name}={(fields.TryGetValue(name, out var value) ? value : string.Empty)}"));
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secretKey), Encoding.UTF8.GetBytes(rawSignature));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public bool VerifySignature(
        IReadOnlyDictionary<string, string> fields,
        string receivedSignature)
    {
        if (string.IsNullOrWhiteSpace(receivedSignature))
        {
            return false;
        }

        var expectedSignature = CreateSignature(fields);
        try
        {
            var expectedBytes = Convert.FromHexString(expectedSignature);
            var receivedBytes = Convert.FromHexString(receivedSignature);
            return expectedBytes.Length == receivedBytes.Length
                && CryptographicOperations.FixedTimeEquals(expectedBytes, receivedBytes);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public IReadOnlyDictionary<string, string> CreatePaymentFields(
        MomoCreatePaymentResult request,
        string partnerCode) =>
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["accessKey"] = options.Value.AccessKey,
            ["amount"] = request.Amount.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["extraData"] = request.ExtraData,
            ["ipnUrl"] = request.IpnUrl,
            ["orderId"] = request.OrderId,
            ["orderInfo"] = request.OrderInfo,
            ["partnerCode"] = partnerCode,
            ["redirectUrl"] = request.RedirectUrl,
            ["requestId"] = request.RequestId,
            ["requestType"] = "captureWallet"
        };

    public IReadOnlyDictionary<string, string> CreateQueryFields(
        MomoQueryPaymentResult request,
        string partnerCode) =>
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["accessKey"] = options.Value.AccessKey,
            ["orderId"] = request.OrderId,
            ["partnerCode"] = partnerCode,
            ["requestId"] = request.RequestId
        };

    public IReadOnlyDictionary<string, string> CreateIpnFields(
        MomoIpnRequest request,
        string accessKey) =>
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["accessKey"] = accessKey,
            ["amount"] = request.Amount.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["extraData"] = request.ExtraData,
            ["message"] = request.Message,
            ["orderId"] = request.OrderId,
            ["orderInfo"] = request.OrderInfo,
            ["orderType"] = request.OrderType,
            ["partnerCode"] = request.PartnerCode,
            ["payType"] = request.PayType,
            ["requestId"] = request.RequestId,
            ["responseTime"] = request.ResponseTime.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["resultCode"] = request.ResultCode.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["transId"] = request.TransactionId.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };

    public IReadOnlyDictionary<string, string> CreateProviderResponseFields(
        MomoProviderPaymentResponse response) =>
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["accessKey"] = options.Value.AccessKey,
            ["amount"] = response.Amount.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["extraData"] = response.ExtraData,
            ["message"] = response.Message,
            ["orderId"] = response.OrderId,
            ["orderInfo"] = response.OrderInfo,
            ["orderType"] = response.OrderType,
            ["partnerCode"] = response.PartnerCode,
            ["payType"] = response.PayType,
            ["requestId"] = response.RequestId,
            ["responseTime"] = response.ResponseTime.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["resultCode"] = response.ResultCode.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["transId"] = response.TransactionId.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };

    private static IReadOnlyList<string> ResolveFieldOrder(IReadOnlyDictionary<string, string> fields)
    {
        if (fields.ContainsKey("requestType"))
        {
            return CreateFieldOrder;
        }

        if (fields.ContainsKey("resultCode"))
        {
            return ProviderResponseFieldOrder;
        }

        if (fields.ContainsKey("orderId") && fields.ContainsKey("requestId"))
        {
            return QueryFieldOrder;
        }

        throw new ArgumentException("The supplied fields do not match a supported MoMo signature contract.", nameof(fields));
    }
}
