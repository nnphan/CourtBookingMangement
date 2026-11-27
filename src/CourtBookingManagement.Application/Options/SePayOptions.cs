using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace CourtBookingManagement.Application.Options;

public sealed class SePayOptions
{
    public const string SectionName = "SePay";

    public string Environment { get; set; } = "Sandbox";
    public string ApiBaseUrl { get; set; } = "https://userapi-sandbox.sepay.vn/v2";
    public string ApiToken { get; set; } = string.Empty;
    public Guid? ApiBankAccountId { get; set; }

    [Range(20, 300)]
    public int ReconciliationIntervalSeconds { get; set; } = 30;

    [Range(1, 168)]
    public int ReconciliationLookbackHours { get; set; } = 24;

    public bool ReconciliationEnabled => IsConfigured && !string.IsNullOrWhiteSpace(ApiToken)
        && ApiBankAccountId is Guid id && id != Guid.Empty
        && Uri.TryCreate(ApiBaseUrl, UriKind.Absolute, out var endpoint)
        && endpoint.Scheme == Uri.UriSchemeHttps && endpoint.IsDefaultPort
        && endpoint.AbsolutePath.TrimEnd('/') == "/v2"
        && string.IsNullOrEmpty(endpoint.Query) && string.IsNullOrEmpty(endpoint.Fragment)
        && string.Equals(endpoint.Host, string.Equals(Environment, "Production", StringComparison.OrdinalIgnoreCase)
            ? "userapi.sepay.vn" : "userapi-sandbox.sepay.vn", StringComparison.OrdinalIgnoreCase);
    public string WebhookAuthenticationMode { get; set; } = "ApiKey";
    public string WebhookApiKey { get; set; } = string.Empty;
    public string WebhookHmacSecret { get; set; } = string.Empty;
    public string BankCode { get; set; } = string.Empty;
    public string BankAccountNumber { get; set; } = string.Empty;
    public string AccountHolderName { get; set; } = string.Empty;
    public string PaymentCodePrefix { get; set; } = "BCM";
    public string QrBaseUrl { get; set; } = "https://vietqr.app/img";
    public string QrTemplate { get; set; } = "compact";
    public string FrontendReturnUrl { get; set; } = "http://localhost:5173/payment/sepay/return";

    [Range(1, 3600)]
    public int PaymentTimeoutSeconds { get; set; } = 300;

    [Range(1, 60)]
    public int RequestTimeoutSeconds { get; set; } = 15;

    [Range(1, 60)]
    public int PaymentStatusPollingSeconds { get; set; } = 3;

    public bool HasAnyPaymentConfiguration =>
        !string.IsNullOrWhiteSpace(BankCode)
        || !string.IsNullOrWhiteSpace(BankAccountNumber)
        || !string.IsNullOrWhiteSpace(AccountHolderName)
        || !string.IsNullOrWhiteSpace(WebhookApiKey)
        || !string.IsNullOrWhiteSpace(WebhookHmacSecret);

    public bool IsConfigured =>
        IsValidConfiguration()
        && !string.IsNullOrWhiteSpace(BankCode)
        && !string.IsNullOrWhiteSpace(BankAccountNumber)
        && !string.IsNullOrWhiteSpace(AccountHolderName);

    public bool IsValidConfiguration()
    {
        var urlsAreValid = IsAbsoluteHttpUrl(ApiBaseUrl)
            && IsAbsoluteHttpUrl(QrBaseUrl)
            && IsAbsoluteHttpUrl(FrontendReturnUrl);
        var isProduction = string.Equals(Environment, "Production", StringComparison.OrdinalIgnoreCase);
        var usesHttpsInProduction = !isProduction
            || (UsesHttps(ApiBaseUrl) && UsesHttps(QrBaseUrl) && UsesHttps(FrontendReturnUrl));
        var authIsValid = string.Equals(WebhookAuthenticationMode, "ApiKey", StringComparison.OrdinalIgnoreCase)
            ? !string.IsNullOrWhiteSpace(WebhookApiKey)
            : string.Equals(WebhookAuthenticationMode, "Hmac", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(WebhookHmacSecret);

        return (string.Equals(Environment, "Sandbox", StringComparison.OrdinalIgnoreCase) || isProduction)
            && urlsAreValid
            && usesHttpsInProduction
            && Regex.IsMatch(BankCode, "^[A-Za-z0-9]{2,20}$")
            && Regex.IsMatch(BankAccountNumber, "^[0-9]{4,34}$")
            && !string.IsNullOrWhiteSpace(AccountHolderName)
            && Regex.IsMatch(PaymentCodePrefix, "^[A-Z]{2,6}$")
            && string.Equals(QrTemplate, "compact", StringComparison.OrdinalIgnoreCase)
            && PaymentTimeoutSeconds > 0
            && RequestTimeoutSeconds > 0
            && PaymentStatusPollingSeconds > 0
            && authIsValid;
    }

    private static bool IsAbsoluteHttpUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
        && string.IsNullOrEmpty(uri.UserInfo);

    private static bool UsesHttps(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps;
}
