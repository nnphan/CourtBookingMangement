using CourtBookingManagement.Application.Payments.Contracts;
using CourtBookingManagement.Domain.Abstractions;

namespace CourtBookingManagement.Application.Payments.Interfaces;

public interface ISePayWebhookService
{
    Task<Result<SePayWebhookResponse>> ProcessAsync(
        ReadOnlyMemory<byte> rawBody,
        string? authorizationHeader,
        string? signatureHeader,
        string? timestampHeader,
        CancellationToken cancellationToken);
}
