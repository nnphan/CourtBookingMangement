using System.Data;
using CourtBookingManagement.Application.Abstractions.Data;
using CourtBookingManagement.Application.UserRoles.Models.Requests;
using CourtBookingManagement.Application.UserRoles.Models.Responses;
using CourtBookingManagement.Application.UserRoles.Repositories;
using CourtBookingManagement.Domain.Abstractions;

namespace CourtBookingManagement.Application.UserRoles.Services;

public sealed class UserRoleService(
    IUserRoleRepository repository,
    ISqlConnectionFactory sqlConnectionFactory) : IUserRoleService
{
    private static readonly Error InvalidUserId = new("USER_ROLE.INVALID_USER_ID", "A valid user id is required.");

    private static readonly Error UserNotFound = new("User.NotFound", "User not found.");

    public async Task<Result<IReadOnlyList<UserRoleResponse>>> GetUserRolesAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty)
        {
            return Result.Failure<IReadOnlyList<UserRoleResponse>>(InvalidUserId);
        }

        try
        {
            if (!await repository.UserExistsAsync(userId, cancellationToken))
            {
                return Result.Failure<IReadOnlyList<UserRoleResponse>>(UserNotFound);
            }

            return Result.Success(await repository.GetUserRolesAsync(userId, cancellationToken));
        }
        catch (Exception exception)
        {
            return Result.Failure<IReadOnlyList<UserRoleResponse>>(Error.FromException(exception));
        }
    }

    public async Task<Result<UserRoleAssignmentResponse>> AssignRolesAsync(
        Guid userId,
        AssignUserRolesRequest request,
        Guid assignedBy,
        CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty)
        {
            return Result.Failure<UserRoleAssignmentResponse>(InvalidUserId);
        }

        if (assignedBy == Guid.Empty)
        {
            return Result.Failure<UserRoleAssignmentResponse>(
                new Error("USER_ROLE.INVALID_ASSIGNER", "The current user is invalid."));
        }

        if (request?.RoleIds is not { Count: > 0 })
        {
            return Result.Failure<UserRoleAssignmentResponse>(
                new Error("USER_ROLE.ROLE_IDS_REQUIRED", "RoleIds is required."));
        }

        var requestedRoleIds = request.RoleIds.Distinct().ToList();
        var invalidRoles = new Error("USER_ROLE.INVALID_ROLES", "One or more roles are invalid.");

        if (requestedRoleIds.Contains(Guid.Empty))
        {
            return Result.Failure<UserRoleAssignmentResponse>(invalidRoles);
        }

        try
        {
            if (!await repository.UserExistsAsync(userId, cancellationToken))
            {
                return Result.Failure<UserRoleAssignmentResponse>(UserNotFound);
            }

            var activeRoles = await repository.GetActiveRolesAsync(requestedRoleIds, cancellationToken);
            if (activeRoles.Count != requestedRoleIds.Count)
            {
                return Result.Failure<UserRoleAssignmentResponse>(invalidRoles);
            }

            var assignedRoleIds = (await repository.GetAssignedRoleIdsAsync(userId, cancellationToken)).ToHashSet();
            var newRoleIds = requestedRoleIds.Where(roleId => !assignedRoleIds.Contains(roleId)).ToList();

            if (newRoleIds.Count > 0)
            {
                await AssignInTransactionAsync(userId, newRoleIds, assignedBy, cancellationToken);
            }

            var assignment = await repository.GetUserRoleAssignmentAsync(userId, cancellationToken);
            return assignment is null
                ? Result.Failure<UserRoleAssignmentResponse>(UserNotFound)
                : Result.Success(assignment);
        }
        catch (Exception exception)
        {
            return Result.Failure<UserRoleAssignmentResponse>(Error.FromException(exception));
        }
    }

    private async Task AssignInTransactionAsync(
        Guid userId,
        IReadOnlyCollection<Guid> roleIds,
        Guid assignedBy,
        CancellationToken cancellationToken)
    {
        using var connection = sqlConnectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);

        try
        {
            await repository.AssignRolesAsync(userId, roleIds, assignedBy, transaction, cancellationToken);
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }
}
