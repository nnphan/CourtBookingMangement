using CourtBookingManagement.Application.Payments.Contracts;
using CourtBookingManagement.Domain.Abstractions;

namespace CourtBookingManagement.Application.Payments.Interfaces;

public interface IPaymentService
{
    Task<Result<CreateMomoPaymentResponse>> CreateMomoPaymentAsync(
        CreateMomoPaymentRequest request,
        string? holdToken,
        CancellationToken cancellationToken);

    Task<Result<GetPaymentStatusResponse>> GetPaymentStatusAsync(
        Guid paymentId,
        string? holdToken,
        CancellationToken cancellationToken);

    Task<Result<GetPaymentStatusResponse>> GetBookingPaymentStatusAsync(
        Guid bookingId,
        string? holdToken,
        CancellationToken cancellationToken);

    Task<Result<GetPaymentStatusResponse>> QueryMomoPaymentAsync(
        Guid paymentId,
        string? holdToken,
        CancellationToken cancellationToken);
}
