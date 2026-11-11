using CourtBookingManagement.Application.Abstractions.Data;
using CourtBookingManagement.Application.Auth.Interfaces;
using CourtBookingManagement.Application.Auth.Exceptions;
using Dapper;

namespace CourtBookingManagement.Infrastructure.Auth;

public sealed class BranchAuthorizationService(ISqlConnectionFactory connectionFactory)
    : IBranchAuthorizationService
{
    public async Task EnsureCanAccessAsync(
        Guid userId,
        Guid branchId,
        string permission,
        CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        var allowed = await connection.ExecuteScalarAsync<bool>(new CommandDefinition("""
            WITH permitted_roles AS (
                SELECT r.id
                FROM auth.roles r
                JOIN auth.user_roles ur ON ur.role_id = r.id
                WHERE ur.user_id = @UserId
                  AND r.code IN ('ADMIN', 'SUPER_ADMIN')
                  AND r.is_active
                  AND r.deleted_at IS NULL

                UNION

                SELECT r.id
                FROM auth.branch_user_roles bur
                JOIN auth.roles r ON r.id = bur.role_id
                WHERE bur.user_id = @UserId
                  AND bur.branch_id = @BranchId
                  AND bur.is_active
                  AND r.code IN ('BRANCH_ADMIN', 'STAFF')
                  AND r.is_active
                  AND r.deleted_at IS NULL

                UNION

                SELECT r.id
                FROM auth.roles r
                JOIN core.branches b ON b.id = @BranchId
                JOIN core.owners o ON o.id = b.owner_id
                WHERE o.user_id = @UserId
                  AND o.is_active
                  AND o.deleted_at IS NULL
                  AND r.code = 'BRANCH_ADMIN'
                  AND r.is_active
                  AND r.deleted_at IS NULL
            )
            SELECT EXISTS (
                SELECT 1
                FROM permitted_roles pr
                JOIN auth.role_permissions rp ON rp.role_id = pr.id
                JOIN auth.permissions p ON p.id = rp.permission_id
                JOIN auth.users u ON u.id = @UserId
                JOIN core.branches b ON b.id = @BranchId
                WHERE p.code = @Permission
                  AND u.is_active
                  AND u.deleted_at IS NULL
                  AND b.is_active
                  AND b.deleted_at IS NULL
            );
            """, new { UserId = userId, BranchId = branchId, Permission = permission },
            cancellationToken: cancellationToken));

        if (!allowed)
        {
            throw new BranchAccessDeniedException();
        }
    }
}
