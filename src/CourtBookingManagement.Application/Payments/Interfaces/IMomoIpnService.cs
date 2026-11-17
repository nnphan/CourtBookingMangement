using CourtBookingManagement.Application.Payments.Contracts;

namespace CourtBookingManagement.Application.Payments.Interfaces;

public interface IMomoIpnService
{
    Task<MomoIpnResponse> ProcessAsync(
        MomoIpnRequest request,
        CancellationToken cancellationToken);
}
