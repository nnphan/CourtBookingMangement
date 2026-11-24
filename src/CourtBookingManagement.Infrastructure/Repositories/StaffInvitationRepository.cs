using CourtBookingManagement.Application.Abstractions.Data;
using CourtBookingManagement.Application.StaffManagement.Interfaces;
using CourtBookingManagement.Application.StaffManagement.Models;
using Dapper;

namespace CourtBookingManagement.Infrastructure.Repositories;

public sealed class StaffInvitationRepository(ISqlConnectionFactory sqlConnectionFactory)
    : IStaffInvitationRepository
{
    public async Task<StaffInvitationState?> GetStateByTokenHashAsync(
        string tokenHash,
        CancellationToken cancellationToken)
    {
        using var connection = sqlConnectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<StaffInvitationState>(new CommandDefinition("""
            WITH database_time AS MATERIALIZED (
                SELECT clock_timestamp() AS value
            )
            SELECT invitation.id AS "InvitationId",
                   invitation.user_id AS "UserId",
                   user_row.email AS "Email",
                   COALESCE(user_row.full_name, '') AS "DisplayName",
                   invitation.expires_at AS "ExpiresAt",
                   database_time.value AS "DatabaseTime",
                   invitation.used_at AS "UsedAt",
                   invitation.revoked_at AS "RevokedAt",
                   invitation.link_version AS "LinkVersion",
                   invitation.issuance_type AS "IssuanceType",
                   user_row.is_active AS "UserIsActive",
                   user_row.is_email_verified AS "UserIsEmailVerified",
                   EXISTS (
                       SELECT 1
                       FROM auth.branch_user_roles assignment
                       JOIN auth.roles role_row ON role_row.id = assignment.role_id
                       JOIN core.branches branch_row ON branch_row.id = assignment.branch_id
                       WHERE assignment.user_id = invitation.user_id
                         AND assignment.is_active
                         AND assignment.removed_at IS NULL
                         AND role_row.code = 'STAFF'
                         AND role_row.is_active
                         AND role_row.deleted_at IS NULL
                         AND branch_row.is_active
                         AND branch_row.deleted_at IS NULL
                   ) AS "HasActiveStaffAssignment",
                   EXISTS (
                       SELECT 1
                       FROM auth.user_roles user_role
                       JOIN auth.roles role_row ON role_row.id = user_role.role_id
                       WHERE user_role.user_id = invitation.user_id
                         AND role_row.is_active
                         AND role_row.deleted_at IS NULL
                         AND role_row.code <> 'STAFF'
                   ) OR EXISTS (
                       SELECT 1
                       FROM auth.branch_user_roles assignment
                       JOIN auth.roles role_row ON role_row.id = assignment.role_id
                       WHERE assignment.user_id = invitation.user_id
                         AND assignment.is_active
                         AND assignment.removed_at IS NULL
                         AND role_row.is_active
                         AND role_row.deleted_at IS NULL
                         AND role_row.code <> 'STAFF'
                   ) AS "HasNonStaffRole",
                   ARRAY(
                       SELECT DISTINCT branch_row.name
                       FROM auth.branch_user_roles assignment
                       JOIN auth.roles role_row ON role_row.id = assignment.role_id
                       JOIN core.branches branch_row ON branch_row.id = assignment.branch_id
                       WHERE assignment.user_id = invitation.user_id
                         AND assignment.is_active
                         AND assignment.removed_at IS NULL
                         AND role_row.code = 'STAFF'
                         AND role_row.is_active
                         AND role_row.deleted_at IS NULL
                         AND branch_row.is_active
                         AND branch_row.deleted_at IS NULL
                       ORDER BY branch_row.name
                   ) AS "BranchNames"
            FROM auth.staff_invitations invitation
            JOIN auth.users user_row ON user_row.id = invitation.user_id
            CROSS JOIN database_time
            WHERE invitation.token_hash = @TokenHash
              AND invitation.purpose = 'STAFF_ACTIVATION'
              AND invitation.link_version = 2
              AND user_row.deleted_at IS NULL;
            """, new { TokenHash = tokenHash }, cancellationToken: cancellationToken));
    }
}
