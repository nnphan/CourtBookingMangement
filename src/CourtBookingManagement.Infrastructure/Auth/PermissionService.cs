using CourtBookingManagement.Application.Abstractions.Data;
using CourtBookingManagement.Application.Auth.Interfaces;
using Dapper;

namespace CourtBookingManagement.Infrastructure.Auth;

public sealed class PermissionService(ISqlConnectionFactory connectionFactory) : IPermissionService
{
    private const string EffectiveRoles = """
        WITH effective_roles AS (
            SELECT r.id, r.code, false AS branch_scoped
            FROM auth.user_roles assignment
            JOIN auth.roles r ON r.id = assignment.role_id
            WHERE assignment.user_id = @UserId AND r.is_active AND r.deleted_at IS NULL
            UNION
            SELECT r.id, r.code, true AS branch_scoped
            FROM auth.branch_user_roles assignment
            JOIN auth.roles r ON r.id = assignment.role_id
            JOIN core.branches branch ON branch.id = assignment.branch_id
            WHERE assignment.user_id = @UserId AND assignment.is_active AND assignment.removed_at IS NULL
              AND r.code = 'BRANCH_MANAGER' AND r.is_active AND r.deleted_at IS NULL
              AND branch.is_active AND branch.deleted_at IS NULL
        )

        """;

    public async Task<IReadOnlyCollection<string>> GetUserPermissionsAsync(
        Guid userId, CancellationToken cancellationToken = default)
    {
        // The claim permits opening a workspace; branch APIs still authorize each requested branch.
        using var connection = connectionFactory.CreateConnection();
        var permissions = await connection.QueryAsync<string>(new CommandDefinition(EffectiveRoles + """
            SELECT DISTINCT p.code
            FROM effective_roles r
            JOIN auth.role_permissions rp ON rp.role_id = r.id
            JOIN auth.permissions p ON p.id = rp.permission_id;
            """, new { UserId = userId }, cancellationToken: cancellationToken));
        //var permissions = await connection.QueryAsync<string>(new CommandDefinition(EffectiveRoles + """
        //    SELECT DISTINCT p.code
        //    FROM effective_roles r
        //    JOIN auth.role_permissions rp ON rp.role_id = r.id
        //    JOIN auth.permissions p ON p.id = rp.permission_id
        //    WHERE NOT r.branch_scoped
        //       OR p.code IN ('dashboard.view', 'booking.view', 'product.view',
        //                     'inventory.view', 'inventory.receive', 'inventory.post_receipt', 'inventory.adjust',
        //                     'sales.view', 'sales.create', 'sales.cancel', 'sales.report.view',
        //                     'sales.invoice.view', 'sales.invoice.print');
        //    """, new { UserId = userId }, cancellationToken: cancellationToken));
        return permissions.AsList();
    }

    public async Task<IReadOnlyCollection<string>> GetUserRolesAsync(
        Guid userId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        var roles = await connection.QueryAsync<string>(new CommandDefinition(
            EffectiveRoles + "SELECT DISTINCT code FROM effective_roles;",
            new { UserId = userId }, cancellationToken: cancellationToken));
        return roles.AsList();
    }
}
