using CourtBookingManagement.Application.Bookings.DTOs;
using CourtBookingManagement.Domain.Abstractions;

namespace CourtBookingManagement.Application.Bookings.Interfaces;

public interface IBookingService
{
    Task<Result<BookingDetailResponse>> GetByIdAsync(Guid bookingId, CancellationToken cancellationToken);
    Task<Result<BookingDetailResponse>> GetHoldAsync(
        Guid bookingId,
        string? holdToken,
        CancellationToken cancellationToken);
    Task<Result<CancelBookingHoldResponse>> CancelHoldAsync(
        Guid bookingId,
        string? holdToken,
        CancellationToken cancellationToken);
    Task<Result<CreateBookingResponse>> CreateAsync(
        CreateBookingRequest request,
        Guid? idempotencyKey,
        CancellationToken cancellationToken);
    Task<Result<CalculateBookingPriceResponse>> CalculatePriceAsync(CalculateBookingPriceRequest request, CancellationToken cancellationToken);
    Task<Result<IReadOnlyList<MyBookingResponse>>> GetMyBookingsAsync(CancellationToken cancellationToken);
    Task<Result<UpdateBookingStatusResponse>> UpdateStatusAsync(
        Guid bookingId,
        UpdateBookingStatusRequest request,
        CancellationToken cancellationToken);
}