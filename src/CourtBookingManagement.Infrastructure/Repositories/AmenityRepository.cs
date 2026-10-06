using CourtBookingManagement.Application.Branches.Interfaces;
using CourtBookingManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CourtBookingManagement.Infrastructure.Repositories;

public sealed class AmenityRepository(ApplicationDbContext dbContext) : IAmenityRepository
{
    public Task<List<Guid>> GetExistingIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken)
    {
        var requestedIds = ids.Distinct().ToArray();
        return dbContext.Amenities
            .Where(amenity => requestedIds.Contains(amenity.Id))
            .Select(amenity => amenity.Id)
            .ToListAsync(cancellationToken);
    }
}