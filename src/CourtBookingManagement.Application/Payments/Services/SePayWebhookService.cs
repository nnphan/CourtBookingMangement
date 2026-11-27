using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CourtBookingManagement.Application.Bookings.Exceptions;
using CourtBookingManagement.Application.Bookings.Interfaces;
using CourtBookingManagement.Application.Options;
using CourtBookingManagement.Application.Payments.Contracts;
using CourtBookingManagement.Application.Payments.Interfaces;
using CourtBookingManagement.Domain.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CourtBookingManagement.Application.Payments.Services;

public sealed class SePayWebhookService(
    IPaymentRepository paymentRepository,
    ISePayWebhookAuthenticator authenticator,
    IBookingHoldCache holdCache,
    IOptions<SePayOptions> options,
    ILogger<SePayWebhookService> logger) : ISePayWebhookService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<Result<SePayWebhookResponse>> ProcessAsync(
        ReadOnlyMemory<byte> rawBody,
        string? authorizationHeader,
        string? signatureHeader,
        string? timestampHeader,
        CancellationToken cancellationToken)
    {
        if (!authenticator.IsAuthenticated(
            rawBody,
            authorizationHeader,
            signatureHeader,
            timestampHeader))
        {
            return Result.Failure<SePayWebhookResponse>(
                new Error("SePay.WebhookUnauthorized", "Webhook authentication failed."));
        }

        SePayWebhookRequest? request;
        try
        {
            request = JsonSerializer.Deserialize<SePayWebhookRequest>(rawBody.Span, JsonOptions);
        }
        catch (JsonException)
        {
            return Result.Failure<SePayWebhookResponse>(
                new Error("SePay.WebhookInvalidPayload", "Webhook payload is invalid."));
        }

        if (request is null || request.Id < 0)
        {
            return Result.Failure<SePayWebhookResponse>(
                new Error("SePay.WebhookInvalidPayload", "Webhook payload is invalid."));
        }

        var settings = options.Value;
        if (request.Id == 0)
        {
            if (!string.Equals(settings.Environment, "Sandbox", StringComparison.OrdinalIgnoreCase))
            {
                return Result.Failure<SePayWebhookResponse>(
                    new Error("SePay.WebhookInvalidPayload", "Webhook payload is invalid."));
            }

            logger.LogWarning(
                "Authenticated SePay sandbox webhook with transaction ID 0 acknowledged without processing.");
            return Result.Success(new SePayWebhookResponse(true));
        }

        await ApplyTransactionAsync(request, Encoding.UTF8.GetString(rawBody.Span), cancellationToken);
        return Result.Success(new SePayWebhookResponse(true));
    }

    public async Task ApplyReconciledTransactionAsync(
        SePayWebhookRequest request, string rawPayload, CancellationToken cancellationToken)
    {
        if (request.ApiTransactionId is Guid id && id != Guid.Empty
            && !string.IsNullOrWhiteSpace(request.ReferenceCode)
            && ResolvePaymentCode(request, options.Value.PaymentCodePrefix) is not null
            && string.Equals(request.AccountNumber, options.Value.BankAccountNumber, StringComparison.Ordinal))
        {
            await ApplyTransactionAsync(request, rawPayload, cancellationToken);
        }
    }

    private async Task ApplyTransactionAsync(
        SePayWebhookRequest request, string rawPayload, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var paymentCode = ResolvePaymentCode(request, settings.PaymentCodePrefix);
        var transactionDate = ParseTransactionDate(request.TransactionDate);
        var accountMatches = string.Equals(
            request.AccountNumber,
            settings.BankAccountNumber,
            StringComparison.Ordinal);
        var outcome = await paymentRepository.ProcessSePayWebhookAsync(
            request,
            paymentCode,
            transactionDate,
            accountMatches,
            rawPayload,
            cancellationToken);

        if (outcome.IsDuplicate)
        {
            logger.LogInformation(
                "Duplicate SePay webhook acknowledged. SePayTransactionId: {SePayTransactionId}",
                request.TransactionIdentity);
            return;
        }

        if (outcome.BookingId is Guid bookingId && outcome.Resources.Count > 0)
        {
            try
            {
                await holdCache.RemoveAsync(bookingId, outcome.Resources, cancellationToken);
            }
            catch (BookingHoldCacheUnavailableException exception)
            {
                logger.LogWarning(
                    exception,
                    "SePay webhook was committed for booking {BookingId}, but Redis hold keys could not be removed.",
                    bookingId);
            }
        }

        logger.LogInformation(
            "SePay webhook processed. SePayTransactionId: {SePayTransactionId}, BookingId: {BookingId}",
            request.TransactionIdentity,
            outcome.BookingId);
    }

    private static string? ResolvePaymentCode(SePayWebhookRequest request, string prefix)
    {
        var code = request.Code?.Trim();
        var providerCode = code is not null && IsValidPaymentCode(code, prefix)
            ? code.ToUpperInvariant()
            : null;

        var pattern = $@"(?<![A-Z0-9]){Regex.Escape(prefix)}[A-F0-9]{{10}}(?![A-Z0-9])";
        var contentCodes = Regex.Matches(
            request.Content ?? string.Empty,
            pattern,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
            .Select(match => match.Value.ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (contentCodes.Length == 0)
            return providerCode;

        return contentCodes.Length == 1 && (providerCode is null || providerCode == contentCodes[0])
            ? contentCodes[0] : null;
    }

    private static bool IsValidPaymentCode(string code, string prefix) =>
        Regex.IsMatch(
            code,
            $@"^{Regex.Escape(prefix)}[A-F0-9]{{10}}$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static DateTimeOffset? ParseTransactionDate(string? value)
    {
        if (!DateTime.TryParseExact(
            value,
            "yyyy-MM-dd HH:mm:ss",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var localDateTime))
        {
            return null;
        }

        var vietnamTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
        var utcDateTime = TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(localDateTime, DateTimeKind.Unspecified),
            vietnamTimeZone);
        return new DateTimeOffset(utcDateTime);
    }
}
