using CourtBookingManagement.Application.Amenities.Models.Responses;
using CourtBookingManagement.Application.Amenities.Repositories;
using Microsoft.Extensions.Logging;

namespace CourtBookingManagement.Application.Amenities.Services;

public sealed class AmenityService(
    IAmenityListRepository repository,
    ILogger<AmenityService> logger) : IAmenityService
{
    public async Task<IReadOnlyList<AmenityResponse>> GetAllAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            var amenities = await repository.GetAllAsync(cancellationToken);
            logger.LogInformation("Loaded {AmenityCount} amenities", amenities.Count);
            return amenities;
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogError(exception, "Failed to load amenities");
            throw;
        }
    }
}