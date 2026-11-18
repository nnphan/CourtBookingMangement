using CourtBookingManagement.Application.Auth.Interfaces;
using CourtBookingManagement.Application.Bookings.Interfaces;
using CourtBookingManagement.Application.Bookings.Security;
using CourtBookingManagement.Application.Options;
using CourtBookingManagement.Application.Payments.Contracts;
using CourtBookingManagement.Application.Payments.Interfaces;
using CourtBookingManagement.Domain.Abstractions;
using CourtBookingManagement.Domain.Payments;
using Microsoft.Extensions.Options;

namespace CourtBookingManagement.Application.Payments.Services;

public sealed class SePayPaymentService(
    IPaymentRepository paymentRepository,
    ICurrentUserService currentUserService,
    IBookingRepository bookingRepository,
    ISePayPaymentCodeGenerator paymentCodeGenerator,
    ISePayQrUrlBuilder qrUrlBuilder,
    IOptions<SePayOptions> options) : ISePayPaymentService
{
    public async Task<Result<CreateSePayPaymentResponse>> CreatePaymentAsync(
        CreateSePayPaymentRequest request,
        string? holdToken,
        CancellationToken cancellationToken)
    {
        var sePayOptions = options.Value;
        if (!sePayOptions.IsConfigured)
        {
            return Result.Failure<CreateSePayPaymentResponse>(
                new Error("Payment.SePayNotConfigured", "SePay payment is not configured."));
        }

        var customerId = await GetCurrentCustomerIdAsync(cancellationToken);
        var holdTokenHash = BookingHoldToken.Hash(holdToken);
        if (customerId is null && holdTokenHash is null)
        {
            return Result.Failure<CreateSePayPaymentResponse>(
                new Error("Booking.NotFound", "Booking hold not found."));
        }

        var booking = await paymentRepository.GetSePayBookingForPaymentAsync(
            request.BookingId,
            customerId,
            holdTokenHash,
            cancellationToken);
        if (booking is null)
        {
            return Result.Failure<CreateSePayPaymentResponse>(
                new Error("Booking.NotFound", "Booking hold not found."));
        }

        if (!string.Equals(booking.BookingStatus, "PENDING_PAYMENT", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(booking.PaymentMethod, "SEPAY", StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure<CreateSePayPaymentResponse>(
                new Error("Booking.StatusConflict", "The booking is not awaiting SePay payment."));
        }

        if (booking.ExpiresAt is not DateTimeOffset expiresAt || expiresAt <= booking.ServerTime)
        {
            return Result.Failure<CreateSePayPaymentResponse>(
                new Error("Booking.HoldExpired", "The booking hold has expired."));
        }

        if (booking.Amount <= 0m || decimal.Truncate(booking.Amount) != booking.Amount)
        {
            return Result.Failure<CreateSePayPaymentResponse>(
                new Error("Payment.InvalidAmount", "The booking amount must be a positive whole number of VND."));
        }

        var paymentId = Guid.NewGuid();
        var paymentCode = paymentCodeGenerator.Generate(sePayOptions.PaymentCodePrefix);
        var qrUrl = qrUrlBuilder.Build(decimal.ToInt64(booking.Amount), paymentCode, sePayOptions);
        var payment = await paymentRepository.CreateOrGetPendingSePayPaymentAsync(
            request.BookingId,
            customerId,
            holdTokenHash,
            paymentId,
            paymentCode,
            $"SEPAY-{paymentId:N}",
            Guid.NewGuid().ToString("N"),
            qrUrl,
            booking.Amount,
            expiresAt,
            sePayOptions.BankCode,
            sePayOptions.BankAccountNumber,
            cancellationToken);

        if (payment is null)
        {
            return Result.Failure<CreateSePayPaymentResponse>(
                new Error("Booking.StatusConflict", "The booking hold changed or is no longer available."));
        }

        if (payment.Status == PaymentStatuses.PaymentReview)
        {
            return Result.Failure<CreateSePayPaymentResponse>(
                new Error("Payment.StatusConflict", "This payment is under review."));
        }

        if (string.IsNullOrWhiteSpace(payment.PaymentCode)
            || string.IsNullOrWhiteSpace(payment.QrCodeUrl)
            || string.IsNullOrWhiteSpace(payment.BankCode)
            || string.IsNullOrWhiteSpace(payment.BankAccountNumber)
            || payment.ExpiresAt is not DateTimeOffset paymentExpiresAt
            || payment.ServerTime is not DateTimeOffset serverTime)
        {
            return Result.Failure<CreateSePayPaymentResponse>(
                new Error("Payment.StatusConflict", "The SePay payment attempt is incomplete."));
        }

        return Result.Success(new CreateSePayPaymentResponse(
            payment.PaymentId,
            payment.BookingId,
            payment.BookingCode ?? booking.BookingCode,
            payment.PaymentCode,
            payment.Amount,
            payment.Status,
            payment.QrCodeUrl,
            payment.BankCode,
            payment.BankAccountNumber,
            sePayOptions.AccountHolderName,
            paymentExpiresAt,
            serverTime,
            payment.RemainingSeconds ?? Math.Max(
                0,
                (int)Math.Ceiling((paymentExpiresAt - serverTime).TotalSeconds))));
    }

    private async Task<Guid?> GetCurrentCustomerIdAsync(CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not Guid userId || userId == Guid.Empty)
        {
            return null;
        }

        return await bookingRepository.GetCustomerIdByUserIdAsync(userId, cancellationToken);
    }
}
