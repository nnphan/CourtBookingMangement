namespace CourtBookingManagement.Application.Branches.Interfaces;

public interface IAmenityRepository
{
    Task<List<Guid>> GetExistingIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken);
}