using System.Text.Json;
using CourtBookingManagement.Application.Bookings.Exceptions;
using CourtBookingManagement.Application.Bookings.Interfaces;
using CourtBookingManagement.Application.Options;
using CourtBookingManagement.Application.Payments.Contracts;
using CourtBookingManagement.Application.Payments.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CourtBookingManagement.Application.Payments.Services;

public sealed class MomoIpnService(
    IPaymentRepository repository,
    IMomoSignatureService signatureService,
    IBookingHoldCache holdCache,
    IOptions<MomoOptions> momoOptions,
    ILogger<MomoIpnService> logger) : IMomoIpnService
{
    public async Task<MomoIpnResponse> ProcessAsync(
        MomoIpnRequest request,
        CancellationToken cancellationToken)
    {
        var options = momoOptions.Value;
        var signatureVerified = options.IsConfigured
            && string.Equals(request.PartnerCode, options.PartnerCode, StringComparison.Ordinal)
            && signatureService.VerifySignature(
                signatureService.CreateIpnFields(request, options.AccessKey),
                request.Signature);
        var payload = JsonSerializer.Serialize(new
        {
            request.PartnerCode,
            request.OrderId,
            request.RequestId,
            request.Amount,
            request.OrderInfo,
            request.OrderType,
            request.TransactionId,
            request.ResultCode,
            request.Message,
            request.PayType,
            request.ResponseTime,
            request.ExtraData
        });

        var outcome = await repository.ProcessMomoIpnAsync(
            request,
            signatureVerified,
            payload,
            cancellationToken);
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
                    "Payment callback was committed for booking {BookingId}, but Redis hold keys could not be removed.",
                    bookingId);
            }
        }

        return outcome.Response;
    }
}
