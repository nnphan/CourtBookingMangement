using CourtBookingManagement.Application.Matching.Models;
using CourtBookingManagement.Application.Permissions.Models.Requests;
using CourtBookingManagement.Application.Permissions.Models.Responses;
using CourtBookingManagement.Domain.Abstractions;

namespace CourtBookingManagement.Application.Permissions.Services;

public interface IPermissionService
{
    Task<Result<PagedResult<PermissionResponse>>> SearchAsync(
        PermissionSearchRequest request,
        CancellationToken cancellationToken);

    Task<Result<PermissionResponse>> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<Result<PermissionResponse>> CreateAsync(
        CreatePermissionRequest request,
        CancellationToken cancellationToken);

    Task<Result<PermissionResponse>> UpdateAsync(
        Guid id,
        UpdatePermissionRequest request,
        CancellationToken cancellationToken);

    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken);
}