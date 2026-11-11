using CourtBookingManagement.Application.Abstractions.Data;
using CourtBookingManagement.Application.Auth.Interfaces;
using CourtBookingManagement.Infrastructure.Persistence;
using Dapper;
using Microsoft.EntityFrameworkCore;

namespace CourtBookingManagement.Infrastructure.Auth;

public sealed class PermissionService(
    ApplicationDbContext dbContext,
    ISqlConnectionFactory connectionFactory) : IPermissionService
{
    public async Task<IReadOnlyCollection<string>> GetUserPermissionsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var permissions = await dbContext.UserRoles
            .AsNoTracking()
            .Where(ur => ur.UserId == userId)
            .SelectMany(ur => ur.Role.Permissions)
            .Select(p => p.Code)
            .Distinct()
            .ToListAsync(cancellationToken);

        using var connection = connectionFactory.CreateConnection();
        var branchPermissions = await connection.QueryAsync<string>(new CommandDefinition("""
            SELECT DISTINCT p.code
            FROM auth.branch_user_roles bur
            JOIN auth.roles r ON r.id = bur.role_id
            JOIN auth.role_permissions rp ON rp.role_id = r.id
            JOIN auth.permissions p ON p.id = rp.permission_id
            WHERE bur.user_id = @UserId
              AND bur.is_active
              AND r.code IN ('BRANCH_ADMIN', 'STAFF')
              AND r.is_active
              AND r.deleted_at IS NULL

            UNION

            SELECT DISTINCT p.code
            FROM core.branches b
            JOIN core.owners o ON o.id = b.owner_id
            JOIN auth.roles r ON r.code = 'BRANCH_ADMIN'
            JOIN auth.role_permissions rp ON rp.role_id = r.id
            JOIN auth.permissions p ON p.id = rp.permission_id
            WHERE o.user_id = @UserId
              AND o.is_active
              AND o.deleted_at IS NULL
              AND b.is_active
              AND b.deleted_at IS NULL
              AND r.is_active
              AND r.deleted_at IS NULL;
            """, new { UserId = userId }, cancellationToken: cancellationToken));

        return permissions.Concat(branchPermissions).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public async Task<IReadOnlyCollection<string>> GetUserRolesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var roles = await dbContext.UserRoles
            .AsNoTracking()
            .Where(ur => ur.UserId == userId)
            .Select(ur => ur.Role.Code)
            .Distinct()
            .ToListAsync(cancellationToken);

        using var connection = connectionFactory.CreateConnection();
        var branchRoles = await connection.QueryAsync<string>(new CommandDefinition("""
            SELECT DISTINCT r.code
            FROM auth.branch_user_roles bur
            JOIN auth.roles r ON r.id = bur.role_id
            WHERE bur.user_id = @UserId
              AND bur.is_active
              AND r.code IN ('BRANCH_ADMIN', 'STAFF')
              AND r.is_active
              AND r.deleted_at IS NULL

            UNION

            SELECT 'BRANCH_ADMIN'
            FROM core.branches b
            JOIN core.owners o ON o.id = b.owner_id
            WHERE o.user_id = @UserId
              AND o.is_active
              AND o.deleted_at IS NULL
              AND b.is_active
              AND b.deleted_at IS NULL;
            """, new { UserId = userId }, cancellationToken: cancellationToken));

        return roles.Concat(branchRoles).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }
}
