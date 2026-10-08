using CourtBookingManagement.Application.Amenities.Models.Responses;

namespace CourtBookingManagement.Application.Amenities.Repositories;

public interface IAmenityListRepository
{
    Task<IReadOnlyList<AmenityResponse>> GetAllAsync(
        CancellationToken cancellationToken);
}