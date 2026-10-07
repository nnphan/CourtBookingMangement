using System.Data;
using CourtBookingManagement.Application.Abstractions.Data;
using CourtBookingManagement.Application.UserRoles.Models.Responses;
using CourtBookingManagement.Application.UserRoles.Repositories;
using Dapper;

namespace CourtBookingManagement.Infrastructure.Repositories;

public sealed class UserRoleRepository(ISqlConnectionFactory sqlConnectionFactory) : IUserRoleRepository
{
    public async Task<bool> UserExistsAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT EXISTS
            (
                SELECT 1
                FROM auth.users u
                WHERE u.id = @UserId
                  AND u.deleted_at IS NULL
            );
            """;

        using var connection = sqlConnectionFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(sql, new { UserId = userId }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<UserRoleResponse>> GetUserRolesAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                r.id AS RoleId,
                r.code AS RoleCode,
                r.name AS RoleName,
                r.description AS Description,
                r.is_active AS IsActive,
                ur.assigned_at AS AssignedAt,
                ur.assigned_by AS AssignedBy,
                au.full_name AS AssignedByName,
                COUNT(DISTINCT rp.permission_id)::int AS PermissionCount
            FROM auth.user_roles ur
            INNER JOIN auth.roles r ON r.id = ur.role_id
            LEFT JOIN auth.users au ON au.id = ur.assigned_by
            LEFT JOIN auth.role_permissions rp ON rp.role_id = r.id
            WHERE ur.user_id = @UserId
              AND r.deleted_at IS NULL
            GROUP BY
                r.id,
                r.code,
                r.name,
                r.description,
                r.is_active,
                ur.assigned_at,
                ur.assigned_by,
                au.full_name
            ORDER BY r.code;
            """;

        using var connection = sqlConnectionFactory.CreateConnection();
        var items = await connection.QueryAsync<UserRoleResponse>(
            new CommandDefinition(sql, new { UserId = userId }, cancellationToken: cancellationToken));

        return items.AsList();
    }

    public async Task<IReadOnlyList<RoleItemResponse>> GetActiveRolesAsync(
        IReadOnlyCollection<Guid> roleIds,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                r.id AS RoleId,
                r.code AS RoleCode,
                r.name AS RoleName
            FROM auth.roles r
            WHERE r.id = ANY(@RoleIds)
              AND r.deleted_at IS NULL
              AND r.is_active = TRUE;
            """;

        using var connection = sqlConnectionFactory.CreateConnection();
        var items = await connection.QueryAsync<RoleItemResponse>(
            new CommandDefinition(sql, new { RoleIds = roleIds.ToArray() }, cancellationToken: cancellationToken));

        return items.AsList();
    }

    public async Task<IReadOnlyList<Guid>> GetAssignedRoleIdsAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT ur.role_id
            FROM auth.user_roles ur
            WHERE ur.user_id = @UserId;
            """;

        using var connection = sqlConnectionFactory.CreateConnection();
        var items = await connection.QueryAsync<Guid>(
            new CommandDefinition(sql, new { UserId = userId }, cancellationToken: cancellationToken));

        return items.AsList();
    }

    public async Task AssignRolesAsync(
        Guid userId,
        IReadOnlyCollection<Guid> roleIds,
        Guid assignedBy,
        IDbTransaction transaction,
        CancellationToken cancellationToken)
    {
        // ON CONFLICT guards against a concurrent request assigning the same role between the check and the insert.
        const string sql = """
            INSERT INTO auth.user_roles
            (
                user_id,
                role_id,
                assigned_at,
                assigned_by
            )
            SELECT
                @UserId,
                role_id,
                NOW(),
                @AssignedBy
            FROM UNNEST(@RoleIds) AS role_id
            ON CONFLICT (user_id, role_id) DO NOTHING;
            """;

        await transaction.Connection!.ExecuteAsync(new CommandDefinition(
            sql,
            new { UserId = userId, RoleIds = roleIds.ToArray(), AssignedBy = assignedBy },
            transaction,
            cancellationToken: cancellationToken));
    }

    public async Task<UserRoleAssignmentResponse?> GetUserRoleAssignmentAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        const string userSql = """
            SELECT
                u.id AS UserId,
                u.full_name AS FullName,
                u.email AS Email
            FROM auth.users u
            WHERE u.id = @UserId
              AND u.deleted_at IS NULL;
            """;

        const string rolesSql = """
            SELECT
                r.id AS RoleId,
                r.code AS RoleCode,
                r.name AS RoleName
            FROM auth.user_roles ur
            INNER JOIN auth.roles r ON r.id = ur.role_id
            WHERE ur.user_id = @UserId
              AND r.deleted_at IS NULL
            ORDER BY r.code;
            """;

        using var connection = sqlConnectionFactory.CreateConnection();
        var parameters = new { UserId = userId };

        var assignment = await connection.QuerySingleOrDefaultAsync<UserRoleAssignmentResponse>(
            new CommandDefinition(userSql, parameters, cancellationToken: cancellationToken));

        if (assignment is null)
        {
            return null;
        }

        var roles = await connection.QueryAsync<RoleItemResponse>(
            new CommandDefinition(rolesSql, parameters, cancellationToken: cancellationToken));

        assignment.Roles = roles.AsList();
        return assignment;
    }
}
