using System.Data;
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

    Task<IReadOnlyList<RoleItemResponse>> GetActiveRolesAsync(
        IReadOnlyCollection<Guid> roleIds,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<Guid>> GetAssignedRoleIdsAsync(
        Guid userId,
        CancellationToken cancellationToken);

    Task AssignRolesAsync(
        Guid userId,
        IReadOnlyCollection<Guid> roleIds,
        Guid assignedBy,
        IDbTransaction transaction,
        CancellationToken cancellationToken);

    Task<UserRoleAssignmentResponse?> GetUserRoleAssignmentAsync(
        Guid userId,
        CancellationToken cancellationToken);
}
