using CourtBookingManagement.Application.UserRoles.Models.Responses;
using CourtBookingManagement.Domain.Abstractions;

namespace CourtBookingManagement.Application.UserRoles.Services;

public interface IUserRoleService
{
    Task<Result<IReadOnlyList<UserRoleResponse>>> GetUserRolesAsync(
        Guid userId,
        CancellationToken cancellationToken);
}
