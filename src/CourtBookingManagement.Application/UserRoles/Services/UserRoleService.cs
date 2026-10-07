using CourtBookingManagement.Application.UserRoles.Models.Responses;
using CourtBookingManagement.Application.UserRoles.Repositories;
using CourtBookingManagement.Domain.Abstractions;

namespace CourtBookingManagement.Application.UserRoles.Services;

public sealed class UserRoleService(IUserRoleRepository repository) : IUserRoleService
{
    public async Task<Result<IReadOnlyList<UserRoleResponse>>> GetUserRolesAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty)
        {
            return Result.Failure<IReadOnlyList<UserRoleResponse>>(
                new Error("USER_ROLE.INVALID_USER_ID", "A valid user id is required."));
        }

        try
        {
            if (!await repository.UserExistsAsync(userId, cancellationToken))
            {
                return Result.Failure<IReadOnlyList<UserRoleResponse>>(
                    new Error("User.NotFound", "User not found."));
            }

            return Result.Success(await repository.GetUserRolesAsync(userId, cancellationToken));
        }
        catch (Exception exception)
        {
            return Result.Failure<IReadOnlyList<UserRoleResponse>>(Error.FromException(exception));
        }
    }
}
