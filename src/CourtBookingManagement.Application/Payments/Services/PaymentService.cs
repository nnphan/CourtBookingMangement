using System.Net.Http;
using System.Text.Json;
using CourtBookingManagement.Application.Auth.Interfaces;
using CourtBookingManagement.Application.Bookings.Interfaces;
using CourtBookingManagement.Application.Bookings.Security;
using CourtBookingManagement.Application.Options;
using CourtBookingManagement.Application.Payments.Contracts;
using CourtBookingManagement.Application.Payments.Interfaces;
using CourtBookingManagement.Domain.Abstractions;
using CourtBookingManagement.Domain.Payments;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CourtBookingManagement.Application.Payments.Services;

public sealed class PaymentService(
    IPaymentRepository repository,
    ICurrentUserService currentUserService,
    IBookingRepository bookingRepository,
    IMomoClient momoClient,
    IMomoSignatureService signatureService,
    IMomoIpnService ipnService,
    IOptions<MomoOptions> momoOptions,
    ILogger<PaymentService> logger) : IPaymentService
{
    public async Task<Result<CreateMomoPaymentResponse>> CreateMomoPaymentAsync(
        CreateMomoPaymentRequest request,
        string? holdToken,
        CancellationToken cancellationToken)
    {
        var options = momoOptions.Value;
        if (!options.IsConfigured)
        {
            return Result.Failure<CreateMomoPaymentResponse>(
                new Error("Payment.MomoNotConfigured", "MoMo payment is not configured."));
        }

        var customerId = await GetCurrentCustomerIdAsync(cancellationToken);
        var localPayment = await repository.CreateOrGetPendingMomoPaymentAsync(
            request.BookingId,
            customerId,
            BookingHoldToken.Hash(holdToken),
            Guid.NewGuid(),
            $"BK-{Guid.NewGuid():N}",
            Guid.NewGuid().ToString("N"),
            cancellationToken);
        if (localPayment is null)
        {
            return Result.Failure<CreateMomoPaymentResponse>(
                new Error("Booking.StatusConflict", "The booking hold is unavailable, expired, or cannot be accessed."));
        }

        if (localPayment.Status == PaymentStatuses.PaymentReview)
        {
            return Result.Failure<CreateMomoPaymentResponse>(
                new Error("Payment.StatusConflict", "This payment is under review."));
        }

        if (!localPayment.NeedsProviderRequest)
        {
            return Result.Success(ToResponse(localPayment));
        }

        if (localPayment.Amount <= 0m || decimal.Truncate(localPayment.Amount) != localPayment.Amount)
        {
            return Result.Failure<CreateMomoPaymentResponse>(
                new Error("Payment.InvalidAmount", "The booking amount cannot be processed by MoMo."));
        }

        var amount = decimal.ToInt64(localPayment.Amount);
        var providerRequest = new MomoCreatePaymentResult(
            localPayment.OrderId,
            localPayment.RequestId,
            amount,
            $"Booking payment {request.BookingId:N}",
            BuildReturnUrl(options.RedirectUrl, request.BookingId),
            options.IpnUrl,
            string.Empty,
            string.Empty);
        var fields = signatureService.CreatePaymentFields(providerRequest, options.PartnerCode);
        var signature = signatureService.CreateSignature(fields);

        MomoProviderPaymentResponse providerResponse;
        try
        {
            providerResponse = await momoClient.CreatePaymentAsync(
                providerRequest,
                options.PartnerCode,
                signature,
                cancellationToken);
        }
        catch (Exception exception) when (
            exception is (HttpRequestException or TaskCanceledException or JsonException or InvalidDataException)
            && (exception is not TaskCanceledException || !cancellationToken.IsCancellationRequested))
        {
            logger.LogError(exception, "MoMo payment creation failed for local payment {PaymentId}.", localPayment.PaymentId);
            await repository.SaveMomoResponseAsync(
                localPayment.PaymentId,
                new MomoProviderPaymentResponse
                {
                    OrderId = localPayment.OrderId,
                    RequestId = localPayment.RequestId,
                    Amount = amount,
                    ResultCode = -1,
                    Message = "MoMo payment provider request failed."
                },
                false,
                cancellationToken);
            return Result.Failure<CreateMomoPaymentResponse>(
                new Error("Payment.ProviderUnavailable", "The payment provider is temporarily unavailable."));
        }

        var responseMatches = providerResponse.Amount == amount
            && string.Equals(providerResponse.PartnerCode, options.PartnerCode, StringComparison.Ordinal)
            && string.Equals(providerResponse.OrderId, localPayment.OrderId, StringComparison.Ordinal)
            && string.Equals(providerResponse.RequestId, localPayment.RequestId, StringComparison.Ordinal)
            && (providerResponse.ResultCode != 0
                || !string.IsNullOrWhiteSpace(providerResponse.PayUrl)
                || !string.IsNullOrWhiteSpace(providerResponse.Deeplink)
                || !string.IsNullOrWhiteSpace(providerResponse.QrCodeUrl));
        var signatureVerified = responseMatches
            && !string.IsNullOrWhiteSpace(providerResponse.Signature)
            && signatureService.VerifySignature(
                signatureService.CreateProviderResponseFields(providerResponse),
                providerResponse.Signature);
        await repository.SaveMomoResponseAsync(
            localPayment.PaymentId,
            providerResponse,
            signatureVerified,
            cancellationToken);

        if (!signatureVerified)
        {
            logger.LogError(
                "MoMo response validation failed for payment {PaymentId}.",
                localPayment.PaymentId);
            return Result.Failure<CreateMomoPaymentResponse>(
                new Error("Payment.ProviderResponseInvalid", "The payment provider response could not be verified."));
        }

        if (providerResponse.ResultCode != 0)
        {
            return Result.Failure<CreateMomoPaymentResponse>(
                new Error("Payment.ProviderFailed", "The payment provider could not start this payment."));
        }

        return Result.Success(new CreateMomoPaymentResponse(
            localPayment.PaymentId,
            localPayment.BookingId,
            PaymentStatuses.Processing,
            localPayment.Amount,
            providerResponse.PayUrl,
            providerResponse.Deeplink,
            providerResponse.QrCodeUrl));
    }

    public async Task<Result<GetPaymentStatusResponse>> GetPaymentStatusAsync(
        Guid paymentId,
        string? holdToken,
        CancellationToken cancellationToken)
    {
        var customerId = await GetCurrentCustomerIdAsync(cancellationToken);
        var payment = await repository.GetPaymentAsync(
            paymentId,
            customerId,
            BookingHoldToken.Hash(holdToken),
            cancellationToken);
        return payment is null
            ? Result.Failure<GetPaymentStatusResponse>(
                new Error("Payment.NotFound", "Payment not found."))
            : Result.Success(new GetPaymentStatusResponse(
                payment.PaymentId,
                payment.BookingId,
                payment.Status,
                payment.Amount,
                payment.PayUrl,
                payment.PaidAt));
    }

    public async Task<Result<GetPaymentStatusResponse>> GetBookingPaymentStatusAsync(
        Guid bookingId,
        string? holdToken,
        CancellationToken cancellationToken)
    {
        var customerId = await GetCurrentCustomerIdAsync(cancellationToken);
        var payment = await repository.GetLatestPaymentForBookingAsync(
            bookingId,
            customerId,
            BookingHoldToken.Hash(holdToken),
            cancellationToken);
        return payment is null
            ? Result.Failure<GetPaymentStatusResponse>(
                new Error("Payment.NotFound", "Payment not found."))
            : Result.Success(new GetPaymentStatusResponse(
                payment.PaymentId,
                payment.BookingId,
                payment.Status,
                payment.Amount,
                payment.PayUrl,
                payment.PaidAt));
    }

    public async Task<Result<GetPaymentStatusResponse>> QueryMomoPaymentAsync(
        Guid paymentId,
        string? holdToken,
        CancellationToken cancellationToken)
    {
        var options = momoOptions.Value;
        if (!options.IsConfigured)
        {
            return Result.Failure<GetPaymentStatusResponse>(
                new Error("Payment.MomoNotConfigured", "MoMo payment is not configured."));
        }

        var customerId = await GetCurrentCustomerIdAsync(cancellationToken);
        var payment = await repository.GetPaymentAsync(
            paymentId,
            customerId,
            BookingHoldToken.Hash(holdToken),
            cancellationToken);
        if (payment is null)
        {
            return Result.Failure<GetPaymentStatusResponse>(
                new Error("Payment.NotFound", "Payment not found."));
        }

        if (string.Equals(payment.Status, PaymentStatuses.Paid, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Success(new GetPaymentStatusResponse(
                payment.PaymentId,
                payment.BookingId,
                payment.Status,
                payment.Amount,
                payment.PayUrl,
                payment.PaidAt));
        }

        if (payment.Amount <= 0m || decimal.Truncate(payment.Amount) != payment.Amount)
        {
            return Result.Failure<GetPaymentStatusResponse>(
                new Error("Payment.InvalidAmount", "The payment amount cannot be queried."));
        }

        var query = new MomoQueryPaymentResult(payment.OrderId, payment.RequestId);
        var fields = signatureService.CreateQueryFields(query, options.PartnerCode);
        MomoProviderPaymentResponse providerResponse;
        try
        {
            providerResponse = await momoClient.QueryPaymentAsync(
                query,
                options.PartnerCode,
                signatureService.CreateSignature(fields),
                cancellationToken);
        }
        catch (Exception exception) when (
            exception is (HttpRequestException or JsonException or InvalidDataException or TaskCanceledException)
            && (exception is not TaskCanceledException || !cancellationToken.IsCancellationRequested))
        {
            logger.LogWarning(exception, "MoMo payment query failed for payment {PaymentId}.", paymentId);
            return Result.Failure<GetPaymentStatusResponse>(
                new Error("Payment.ProviderUnavailable", "The payment provider is temporarily unavailable."));
        }

        var amount = decimal.ToInt64(payment.Amount);
        var responseMatches = providerResponse.Amount == amount
            && string.Equals(providerResponse.PartnerCode, options.PartnerCode, StringComparison.Ordinal)
            && string.Equals(providerResponse.OrderId, payment.OrderId, StringComparison.Ordinal)
            && string.Equals(providerResponse.RequestId, payment.RequestId, StringComparison.Ordinal);
        var signatureVerified = responseMatches
            && !string.IsNullOrWhiteSpace(providerResponse.Signature)
            && signatureService.VerifySignature(
                signatureService.CreateProviderResponseFields(providerResponse),
                providerResponse.Signature);
        if (!signatureVerified)
        {
            logger.LogError("MoMo payment query response validation failed for payment {PaymentId}.", paymentId);
            return Result.Failure<GetPaymentStatusResponse>(
                new Error("Payment.ProviderResponseInvalid", "The payment provider response could not be verified."));
        }

        if (providerResponse.ResultCode != 0)
        {
            return Result.Success(new GetPaymentStatusResponse(
                payment.PaymentId,
                payment.BookingId,
                payment.Status,
                payment.Amount,
                payment.PayUrl,
                payment.PaidAt));
        }

        await ipnService.ProcessAsync(new MomoIpnRequest
        {
            PartnerCode = providerResponse.PartnerCode,
            OrderId = providerResponse.OrderId,
            RequestId = providerResponse.RequestId,
            Amount = providerResponse.Amount,
            OrderInfo = providerResponse.OrderInfo,
            OrderType = providerResponse.OrderType,
            TransactionId = providerResponse.TransactionId,
            ResultCode = providerResponse.ResultCode,
            Message = providerResponse.Message,
            PayType = providerResponse.PayType,
            ResponseTime = providerResponse.ResponseTime,
            ExtraData = providerResponse.ExtraData,
            Signature = providerResponse.Signature!
        }, cancellationToken);

        var latestPayment = await repository.GetPaymentAsync(
            paymentId,
            customerId,
            BookingHoldToken.Hash(holdToken),
            cancellationToken) ?? payment;
        return Result.Success(new GetPaymentStatusResponse(
            latestPayment.PaymentId,
            latestPayment.BookingId,
            latestPayment.Status,
            latestPayment.Amount,
            latestPayment.PayUrl,
            latestPayment.PaidAt));
    }

    private async Task<Guid?> GetCurrentCustomerIdAsync(CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not Guid userId || userId == Guid.Empty)
        {
            return null;
        }

        return await bookingRepository.GetCustomerIdByUserIdAsync(userId, cancellationToken);
    }

    private static CreateMomoPaymentResponse ToResponse(PaymentTransactionProjection payment) =>
        new(
            payment.PaymentId,
            payment.BookingId,
            payment.Status,
            payment.Amount,
            payment.PayUrl,
            payment.Deeplink,
            payment.QrCodeUrl);

    private static string BuildReturnUrl(string configuredUrl, Guid bookingId)
    {
        var uriBuilder = new UriBuilder(configuredUrl);
        var query = uriBuilder.Query.TrimStart('?');
        var bookingQuery = $"bookingId={Uri.EscapeDataString(bookingId.ToString())}";
        uriBuilder.Query = string.IsNullOrEmpty(query) ? bookingQuery : $"{query}&{bookingQuery}";
        return uriBuilder.Uri.AbsoluteUri;
    }
}
