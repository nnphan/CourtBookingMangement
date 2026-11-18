using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using CourtBookingManagement.Application.Options;
using CourtBookingManagement.Application.Payments.Interfaces;
using Microsoft.Extensions.Options;

namespace CourtBookingManagement.Application.Payments.Services;

public sealed class SePayWebhookAuthenticator(IOptions<SePayOptions> options)
    : ISePayWebhookAuthenticator
{
    private const string ApiKeyScheme = "Apikey ";
    private const string SignaturePrefix = "sha256=";
    private const long AllowedClockSkewSeconds = 300;

    public bool IsAuthenticated(
        ReadOnlyMemory<byte> rawBody,
        string? authorizationHeader,
        string? signatureHeader,
        string? timestampHeader)
    {
        var settings = options.Value;
        if (!settings.IsConfigured)
        {
            return false;
        }

        if (string.Equals(
            settings.WebhookAuthenticationMode,
            "ApiKey",
            StringComparison.OrdinalIgnoreCase))
        {
            return IsValidApiKey(authorizationHeader, settings.WebhookApiKey);
        }

        return IsValidHmac(
            rawBody.Span,
            signatureHeader,
            timestampHeader,
            settings.WebhookHmacSecret);
    }

    private static bool IsValidApiKey(string? authorizationHeader, string expectedKey)
    {
        if (authorizationHeader is null
            || !authorizationHeader.StartsWith(ApiKeyScheme, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var suppliedKey = authorizationHeader[ApiKeyScheme.Length..];
        return FixedTimeEquals(expectedKey, suppliedKey);
    }

    private static bool IsValidHmac(
        ReadOnlySpan<byte> rawBody,
        string? signatureHeader,
        string? timestampHeader,
        string secret)
    {
        if (string.IsNullOrEmpty(signatureHeader)
            || !signatureHeader.StartsWith(SignaturePrefix, StringComparison.Ordinal)
            || !long.TryParse(
                timestampHeader,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var timestamp))
        {
            return false;
        }

        var currentTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (timestamp < currentTimestamp - AllowedClockSkewSeconds
            || timestamp > currentTimestamp + AllowedClockSkewSeconds)
        {
            return false;
        }

        var timestampPrefix = Encoding.ASCII.GetBytes($"{timestampHeader}.");
        var signedContent = new byte[timestampPrefix.Length + rawBody.Length];
        timestampPrefix.CopyTo(signedContent, 0);
        rawBody.CopyTo(signedContent.AsSpan(timestampPrefix.Length));

        var expectedHash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), signedContent);
        var expectedSignature = SignaturePrefix + Convert.ToHexString(expectedHash).ToLowerInvariant();
        return FixedTimeEquals(expectedSignature, signatureHeader);
    }

    private static bool FixedTimeEquals(string expected, string supplied) =>
        CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(supplied));
}
