using CourtBookingManagement.Application.CourtBookings.DTOs;
using CourtBookingManagement.Domain.Abstractions;

namespace CourtBookingManagement.Application.CourtBookings.Interfaces;

public interface ICourtBookingService
{
    Task<Result<CreateCourtBookingResponse>> CreateAsync(CreateCourtBookingRequest request, CancellationToken cancellationToken);
    Task<Result<CheckCourtAvailabilityResponse>> CheckAvailabilityAsync(CheckCourtAvailabilityRequest request, CancellationToken cancellationToken);
    Task<Result<IReadOnlyList<CreateCourtBookingResponse>>> SearchAsync(SearchCourtBookingsRequest request, CancellationToken cancellationToken);
    Task<Result> CancelAsync(CancelCourtBookingRequest request, CancellationToken cancellationToken);
}