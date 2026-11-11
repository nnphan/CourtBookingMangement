namespace CourtBookingManagement.Application.Auth.Interfaces;

public interface IBranchAuthorizationService
{
    Task EnsureCanAccessAsync(
        Guid userId,
        Guid branchId,
        string permission,
        CancellationToken cancellationToken);
}
