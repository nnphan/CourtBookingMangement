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
}
