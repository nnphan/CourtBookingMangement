using CourtBookingManagement.Application.Bookings.DTOs;
using CourtBookingManagement.Domain.Abstractions;

namespace CourtBookingManagement.Application.Bookings.Interfaces;

public interface IBookingService
{
    Task<Result<BookingDetailResponse>> CreateAsync(CreateBookingRequest request, CancellationToken cancellationToken);
}