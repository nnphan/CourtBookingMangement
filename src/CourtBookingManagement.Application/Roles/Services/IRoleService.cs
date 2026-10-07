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
}