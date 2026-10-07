using CourtBookingManagement.Application.UserRoles.Models.Responses;

namespace CourtBookingManagement.Application.UserRoles.Repositories;

public interface IUserRoleRepository
{
    Task<bool> UserExistsAsync(
        Guid userId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<UserRoleResponse>> GetUserRolesAsync(
        Guid userId,
        CancellationToken cancellationToken);
}
