using CourtBookingManagement.Application.Matching.Models;
using CourtBookingManagement.Application.Permissions.Models.Requests;
using CourtBookingManagement.Application.Permissions.Models.Responses;

namespace CourtBookingManagement.Application.Permissions.Repositories;

public interface IPermissionRepository
{
    Task<PagedResult<PermissionResponse>> SearchAsync(
        PermissionSearchRequest request,
        CancellationToken cancellationToken);
}