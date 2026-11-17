using CourtBookingManagement.Application.Payments.Contracts;

namespace CourtBookingManagement.Application.Payments.Interfaces;

public interface IMomoSignatureService
{
    string CreateSignature(IReadOnlyDictionary<string, string> fields);
    bool VerifySignature(IReadOnlyDictionary<string, string> fields, string receivedSignature);
    IReadOnlyDictionary<string, string> CreatePaymentFields(MomoCreatePaymentResult request, string partnerCode);
    IReadOnlyDictionary<string, string> CreateQueryFields(MomoQueryPaymentResult request, string partnerCode);
    IReadOnlyDictionary<string, string> CreateIpnFields(MomoIpnRequest request, string accessKey);
    IReadOnlyDictionary<string, string> CreateProviderResponseFields(MomoProviderPaymentResponse response);
}
