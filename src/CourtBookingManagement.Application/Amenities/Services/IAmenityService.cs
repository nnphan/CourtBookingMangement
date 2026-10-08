using CourtBookingManagement.Application.Amenities.Models.Responses;

namespace CourtBookingManagement.Application.Amenities.Services;

public interface IAmenityService
{
    Task<IReadOnlyList<AmenityResponse>> GetAllAsync(
        CancellationToken cancellationToken);
}