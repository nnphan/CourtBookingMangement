using CourtBookingManagement.Application.Matching.Models;
using CourtBookingManagement.Application.Roles.Models.Requests;
using CourtBookingManagement.Application.Roles.Models.Responses;

namespace CourtBookingManagement.Application.Roles.Repositories;

public interface IRoleRepository
{
    Task<PagedResult<RoleResponse>> SearchAsync(
        RoleSearchRequest request,
        CancellationToken cancellationToken);
}