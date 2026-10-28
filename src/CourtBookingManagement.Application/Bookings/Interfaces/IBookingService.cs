using CourtBookingManagement.Application.Bookings.DTOs;
using CourtBookingManagement.Domain.Abstractions;

namespace CourtBookingManagement.Application.Bookings.Interfaces;

public interface IBookingService
{
    Task<Result<BookingResponse>> CreateAsync(CreateBookingRequest request, CancellationToken cancellationToken);
    Task<Result<BookingResponse>> CreateForStaffAsync(CreateStaffBookingRequest request, CancellationToken cancellationToken);
    Task<Result<CalculateBookingPriceResponse>> CalculatePriceAsync(CalculateBookingPriceRequest request, CancellationToken cancellationToken);
    Task<Result<IReadOnlyList<MyBookingResponse>>> GetMyBookingsAsync(CancellationToken cancellationToken);
}