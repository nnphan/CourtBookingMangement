using CourtBookingManagement.Application.Payments.Contracts;

namespace CourtBookingManagement.Application.Payments.Interfaces;

public interface IMomoClient
{
    Task<MomoProviderPaymentResponse> CreatePaymentAsync(
        MomoCreatePaymentResult request,
        string partnerCode,
        string signature,
        CancellationToken cancellationToken);

    Task<MomoProviderPaymentResponse> QueryPaymentAsync(
        MomoQueryPaymentResult request,
        string partnerCode,
        string signature,
        CancellationToken cancellationToken);
}
