namespace CourtBookingManagement.Application.Customers.Repositories;

public interface IMembershipLevelRepository
{
    Task<Guid?> GetDefaultMembershipLevelIdAsync(
        CancellationToken cancellationToken);
}
