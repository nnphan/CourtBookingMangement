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
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
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

        var hasRoleIds = request?.RoleIds is { Count: > 0 };

        

        List<Guid> requestedRoleIds;
       
        requestedRoleIds = request!.RoleIds.Distinct().ToList();
        

        if (requestedRoleIds.Contains(Guid.Empty))
        {
            return Result.Failure<UserRoleAssignmentResponse>(
                new Error("USER_ROLE.INVALID_ROLES", "One or more role ids are invalid."));
        }

        try
        {
            using var connection = sqlConnectionFactory.CreateConnection();
            connection.Open();
            using var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);

            if (!await repository.LockUserAsync(userId, transaction, cancellationToken))
            {
                return Result.Failure<UserRoleAssignmentResponse>(UserNotFound);
            }

            var activeRoles = await repository.GetActiveRolesAsync(requestedRoleIds, transaction, cancellationToken);
            if (activeRoles.Count != requestedRoleIds.Count)
            {
                return Result.Failure<UserRoleAssignmentResponse>(
                    new Error("Role.NotFound", "One or more roles were not found or are inactive."));
            }

            await repository.AssignRolesAsync(userId, requestedRoleIds, assignedBy, transaction, cancellationToken);
            var assignment = await repository.GetUserRoleAssignmentAsync(userId, transaction, cancellationToken);
            if (assignment is null)
            {
                return Result.Failure<UserRoleAssignmentResponse>(UserNotFound);
            }

            cancellationToken.ThrowIfCancellationRequested();
            transaction.Commit();
            return Result.Success(assignment);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return Result.Failure<UserRoleAssignmentResponse>(Error.FromException(exception));
        }
    }

    public async Task<Result<UserRoleAssignmentResponse>> UpdateRolesAsync(
        Guid userId,
        UpdateUserRolesRequest request,
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

        if (request?.RoleIds is null || request.RoleIds.Contains(Guid.Empty))
        {
            return Result.Failure<UserRoleAssignmentResponse>(
                new Error("USER_ROLE.INVALID_ROLES", "A valid role id list is required."));
        }

        var roleIds = request.RoleIds.Distinct().ToArray();

        using var connection = sqlConnectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);

        if (!await repository.LockUserAsync(userId, transaction, cancellationToken))
        {
            return Result.Failure<UserRoleAssignmentResponse>(UserNotFound);
        }

        var activeRoles = await repository.GetActiveRolesAsync(roleIds, transaction, cancellationToken);
        if (activeRoles.Count != roleIds.Length)
        {
            return Result.Failure<UserRoleAssignmentResponse>(
                new Error("Role.NotFound", "One or more roles were not found or are inactive."));
        }

        await repository.RemoveOtherRolesAsync(userId, roleIds, transaction, cancellationToken);
        await repository.AssignRolesAsync(userId, roleIds, assignedBy, transaction, cancellationToken);
        var assignment = await repository.GetUserRoleAssignmentAsync(userId, transaction, cancellationToken);
        if (assignment is null)
        {
            return Result.Failure<UserRoleAssignmentResponse>(UserNotFound);
        }

        cancellationToken.ThrowIfCancellationRequested();
        transaction.Commit();
        return Result.Success(assignment);
    }
}
