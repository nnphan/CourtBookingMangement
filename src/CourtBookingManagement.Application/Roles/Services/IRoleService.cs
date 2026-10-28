using CourtBookingManagement.Application.Matching.Models;
using CourtBookingManagement.Application.Roles.Models.Requests;
using CourtBookingManagement.Application.Roles.Models.Responses;
using CourtBookingManagement.Domain.Abstractions;

namespace CourtBookingManagement.Application.Roles.Services;

public interface IRoleService
{
    Task<Result<PagedResult<RoleResponse>>> SearchAsync(
        RoleSearchRequest request,
        CancellationToken cancellationToken);

    Task<Result<RoleResponse>> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<Result<RoleResponse>> CreateAsync(
        CreateRoleRequest request,
        Guid? actorId,
        CancellationToken cancellationToken);

    Task<Result<RoleResponse>> UpdateAsync(
        Guid id,
        UpdateRoleRequest request,
        Guid? actorId,
        CancellationToken cancellationToken);

    Task<Result> DeleteAsync(Guid id, Guid? actorId, CancellationToken cancellationToken);
}