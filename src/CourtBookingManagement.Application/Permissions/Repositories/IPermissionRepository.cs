using CourtBookingManagement.Application.Matching.Models;
using CourtBookingManagement.Application.Permissions.Models.Requests;
using CourtBookingManagement.Application.Permissions.Models.Responses;

namespace CourtBookingManagement.Application.Permissions.Repositories;

public interface IPermissionRepository
{
    Task<PagedResult<PermissionResponse>> SearchAsync(
        PermissionSearchRequest request,
        CancellationToken cancellationToken);

    Task<PermissionResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> ExistsByCodeAsync(string code, CancellationToken cancellationToken);

    Task<PermissionWriteResult> CreateAsync(
        CreatePermissionRequest request,
        CancellationToken cancellationToken);

    Task<PermissionWriteResult> UpdateAsync(
        Guid id,
        UpdatePermissionRequest request,
        CancellationToken cancellationToken);

    Task<PermissionDeleteStatus> DeleteAsync(Guid id, CancellationToken cancellationToken);
}