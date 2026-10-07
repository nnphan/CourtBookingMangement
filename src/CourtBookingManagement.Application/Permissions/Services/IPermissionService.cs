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
}