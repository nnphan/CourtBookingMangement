using CourtBookingManagement.Application.Payments.Contracts;
using CourtBookingManagement.Domain.Abstractions;

namespace CourtBookingManagement.Application.Payments.Interfaces;

public interface ISePayPaymentService
{
    Task<Result<CreateSePayPaymentResponse>> CreatePaymentAsync(
        CreateSePayPaymentRequest request,
        string? holdToken,
        CancellationToken cancellationToken);
}
