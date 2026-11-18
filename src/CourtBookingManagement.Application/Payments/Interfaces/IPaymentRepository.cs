using CourtBookingManagement.Application.Payments.Contracts;

namespace CourtBookingManagement.Application.Payments.Interfaces;

public interface IPaymentRepository
{
    Task<SePayBookingPaymentContext?> GetSePayBookingForPaymentAsync(
        Guid bookingId,
        Guid? customerId,
        byte[]? holdTokenHash,
        CancellationToken cancellationToken);

    Task<PaymentTransactionProjection?> CreateOrGetPendingSePayPaymentAsync(
        Guid bookingId,
        Guid? customerId,
        byte[]? holdTokenHash,
        Guid paymentId,
        string paymentCode,
        string orderId,
        string requestId,
        string qrUrl,
        decimal expectedAmount,
        DateTimeOffset expectedExpiresAt,
        string bankCode,
        string bankAccountNumber,
        CancellationToken cancellationToken);

    Task<SePayWebhookProcessingOutcome> ProcessSePayWebhookAsync(
        SePayWebhookRequest request,
        string? paymentCode,
        DateTimeOffset? transactionDate,
        bool receivingAccountMatches,
        string rawPayload,
        CancellationToken cancellationToken);

    Task<PaymentTransactionProjection?> CreateOrGetPendingMomoPaymentAsync(
        Guid bookingId,
        Guid? customerId,
        byte[]? holdTokenHash,
        Guid paymentId,
        string orderId,
        string requestId,
        CancellationToken cancellationToken);

    Task<PaymentTransactionProjection?> GetPaymentAsync(
        Guid paymentId,
        Guid? customerId,
        byte[]? holdTokenHash,
        CancellationToken cancellationToken);

    Task<PaymentTransactionProjection?> GetLatestPaymentForBookingAsync(
        Guid bookingId,
        Guid? customerId,
        byte[]? holdTokenHash,
        CancellationToken cancellationToken);

    Task SaveMomoResponseAsync(
        Guid paymentId,
        MomoProviderPaymentResponse response,
        bool signatureVerified,
        CancellationToken cancellationToken);

    Task<MomoIpnOutcome> ProcessMomoIpnAsync(
        MomoIpnRequest request,
        bool signatureVerified,
        string sanitizedPayload,
        CancellationToken cancellationToken);
}
