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

        var paymentCode = ResolvePaymentCode(request, settings.PaymentCodePrefix);
        var transactionDate = ParseTransactionDate(request.TransactionDate);
        var accountMatches = string.Equals(
            request.AccountNumber,
            settings.BankAccountNumber,
            StringComparison.Ordinal);
        var rawPayload = Encoding.UTF8.GetString(rawBody.Span);

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
                request.Id);
            return Result.Success(new SePayWebhookResponse(true));
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
            request.Id,
            outcome.BookingId);
        return Result.Success(new SePayWebhookResponse(true));
    }

    private static string? ResolvePaymentCode(SePayWebhookRequest request, string prefix)
    {
        if (request.Code is not null)
        {
            return IsValidPaymentCode(request.Code, prefix)
                ? request.Code.ToUpperInvariant()
                : null;
        }

        if (string.IsNullOrEmpty(request.Content))
        {
            return null;
        }

        var pattern = $@"(?<![A-Z0-9]){Regex.Escape(prefix)}[A-F0-9]{{10}}(?![A-Z0-9])";
        var matches = Regex.Matches(
            request.Content,
            pattern,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return matches.Count == 1
            ? matches[0].Value.ToUpperInvariant()
            : null;
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
