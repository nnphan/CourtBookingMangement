namespace CourtBookingManagement.Application.Payments.Interfaces;

public interface ISePayWebhookAuthenticator
{
    bool IsAuthenticated(
        ReadOnlyMemory<byte> rawBody,
        string? authorizationHeader,
        string? signatureHeader,
        string? timestampHeader);
}
