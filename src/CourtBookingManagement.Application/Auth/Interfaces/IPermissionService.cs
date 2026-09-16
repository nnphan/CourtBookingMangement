namespace CourtBookingManagement.Application.Auth.Interfaces;

public interface IPermissionService
{
    Task<IReadOnlyCollection<string>> GetUserPermissionsAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<string>> GetUserRolesAsync(Guid userId, CancellationToken cancellationToken = default);
}
