using CourtBookingManagement.Application.Matching.Models;
using CourtBookingManagement.Application.Roles.Models.Requests;
using CourtBookingManagement.Application.Roles.Models.Responses;

namespace CourtBookingManagement.Application.Roles.Repositories;

public interface IRoleRepository
{
    Task<PagedResult<RoleResponse>> SearchAsync(
        RoleSearchRequest request,
        CancellationToken cancellationToken);

    Task<Guid?> GetRoleIdByCodeAsync(
        string roleCode,
        CancellationToken cancellationToken);

    Task<RoleResponse?> GetByIdWithPermissionsAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> ExistsByCodeAsync(
        string code,
        Guid? excludeId,
        CancellationToken cancellationToken);

    Task<RoleWriteResult> CreateAsync(
        CreateRoleRequest request,
        Guid? actorId,
        CancellationToken cancellationToken);

    Task<RoleWriteResult> UpdateAsync(
        Guid id,
        UpdateRoleRequest request,
        Guid? actorId,
        CancellationToken cancellationToken);

    Task<RoleDeleteStatus> DeleteAsync(
        Guid id,
        Guid? actorId,
        CancellationToken cancellationToken);
}