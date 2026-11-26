using CourtBookingManagement.Application.Abstractions.Data;
using CourtBookingManagement.Application.Matching.Models;
using CourtBookingManagement.Application.StaffManagement.DTOs;
using CourtBookingManagement.Application.StaffManagement.Interfaces;
using CourtBookingManagement.Application.StaffManagement.Models;
using Dapper;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace CourtBookingManagement.Infrastructure.Repositories;

public sealed class BranchOwnerStaffRepository(
    ISqlConnectionFactory sqlConnectionFactory,
    ILogger<BranchOwnerStaffRepository> logger)
    : IBranchOwnerStaffRepository
{
    public async Task<StaffInvitationIssueResult> IssueInvitationAsync(
        Guid ownerId,
        Guid actorId,
        Guid userId,
        Guid invitationId,
        string tokenHash,
        DateTimeOffset expiresAt,
        int resendCooldownMinutes,
        int maxResendsPerWindow,
        int resendWindowMinutes,
        CancellationToken cancellationToken)
    {
        using var connection = sqlConnectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        var invitation = await connection.QuerySingleOrDefaultAsync<StaffInvitation>(new CommandDefinition("""
            SELECT @InvitationId AS "InvitationId",
                   u.email AS "Email",
                   COALESCE(u.full_name, '') AS "DisplayName",
                   u.is_active AS "UserIsActive",
                   u.is_email_verified AS "UserIsEmailVerified",
                   ARRAY(
                       SELECT DISTINCT b.name
                       FROM auth.branch_user_roles bur
                       JOIN core.branches b ON b.id = bur.branch_id
                       JOIN auth.roles r ON r.id = bur.role_id
                       WHERE bur.user_id = u.id
                         AND bur.is_active AND bur.removed_at IS NULL
                         AND b.owner_id = o.id AND b.is_active AND b.deleted_at IS NULL
                         AND r.code = 'STAFF' AND r.is_active AND r.deleted_at IS NULL
                       ORDER BY b.name
                   ) AS "BranchNames"
            FROM auth.users u
            JOIN core.owners o ON o.id = @OwnerId
            JOIN auth.users actor ON actor.id = o.user_id
            WHERE u.id = @UserId AND u.created_by = @ActorId
              AND u.deleted_at IS NULL
              AND o.user_id = @ActorId AND o.is_active AND o.deleted_at IS NULL
              AND actor.is_active AND actor.deleted_at IS NULL
              AND EXISTS (
                  SELECT 1 FROM auth.branch_user_roles bur
                  JOIN core.branches b ON b.id = bur.branch_id
                  JOIN auth.roles r ON r.id = bur.role_id
                  WHERE bur.user_id = u.id AND bur.is_active AND bur.removed_at IS NULL
                    AND b.owner_id = o.id AND b.is_active AND b.deleted_at IS NULL
                    AND r.code = 'STAFF' AND r.is_active AND r.deleted_at IS NULL
              )
              AND NOT EXISTS (
                  SELECT 1 FROM auth.branch_user_roles bur
                  JOIN core.branches b ON b.id = bur.branch_id
                  WHERE bur.user_id = u.id AND bur.is_active AND bur.removed_at IS NULL
                    AND b.owner_id <> o.id
              )
              AND NOT EXISTS (
                  SELECT 1 FROM auth.user_roles ur
                  JOIN auth.roles r ON r.id = ur.role_id
                  WHERE ur.user_id = u.id AND r.is_active AND r.deleted_at IS NULL
                    AND r.code <> 'STAFF'
              )
              AND NOT EXISTS (
                  SELECT 1 FROM auth.branch_user_roles bur
                  JOIN auth.roles r ON r.id = bur.role_id
                  WHERE bur.user_id = u.id AND bur.is_active AND bur.removed_at IS NULL AND r.is_active
                    AND r.deleted_at IS NULL AND r.code <> 'STAFF'
              )
              AND EXISTS (
                  SELECT 1 FROM auth.roles r
                  JOIN auth.role_permissions rp ON rp.role_id = r.id
                  JOIN auth.permissions p ON p.id = rp.permission_id
                  WHERE r.code = 'BRANCH_OWNER' AND r.is_active AND r.deleted_at IS NULL
                    AND p.code = 'staff.create'
              )
            FOR UPDATE OF u, o, actor;
            """, new { OwnerId = ownerId, ActorId = actorId, UserId = userId, InvitationId = invitationId },
            transaction, cancellationToken: cancellationToken));

        if (invitation is null)
        {
            transaction.Rollback();
            return new StaffInvitationIssueResult(StaffInvitationIssueOutcome.NotFound);
        }

        if (invitation.UserIsActive)
        {
            transaction.Rollback();
            return new StaffInvitationIssueResult(StaffInvitationIssueOutcome.AlreadyActive);
        }

        if (invitation.UserIsEmailVerified)
        {
            transaction.Rollback();
            return new StaffInvitationIssueResult(StaffInvitationIssueOutcome.NotFound);
        }

        var databaseTime = await connection.QuerySingleAsync<DateTimeOffset>(new CommandDefinition(
            "SELECT clock_timestamp();",
            transaction: transaction,
            cancellationToken: cancellationToken));
        var latestIssuedAt = await connection.QuerySingleOrDefaultAsync<DateTimeOffset?>(
            new CommandDefinition("""
                SELECT MAX(created_at)
                FROM auth.staff_invitations
                WHERE user_id = @UserId AND purpose = 'STAFF_ACTIVATION';
                """, new { UserId = userId }, transaction, cancellationToken: cancellationToken));

        if (latestIssuedAt is DateTimeOffset latest &&
            latest.AddMinutes(resendCooldownMinutes) > databaseTime)
        {
            transaction.Rollback();
            return new StaffInvitationIssueResult(
                StaffInvitationIssueOutcome.Cooldown,
                RetryAfterSeconds: GetRetryAfterSeconds(
                    latest.AddMinutes(resendCooldownMinutes), databaseTime));
        }

        var resendWindowStart = databaseTime.AddMinutes(-resendWindowMinutes);
        var resendWindow = await connection.QuerySingleAsync<ResendWindow>(new CommandDefinition("""
            SELECT COUNT(*)::int AS "ResendCount",
                   MIN(created_at) AS "OldestResendAt"
            FROM auth.staff_invitations
            WHERE user_id = @UserId
              AND purpose = 'STAFF_ACTIVATION'
              AND created_at >= @WindowStart
              AND (issuance_type = 'RESEND' OR issuance_type IS NULL);
            """, new { UserId = userId, WindowStart = resendWindowStart },
            transaction, cancellationToken: cancellationToken));

        if (resendWindow.ResendCount >= maxResendsPerWindow)
        {
            var oldestResendAt = resendWindow.OldestResendAt
                ?? throw new InvalidOperationException("The resend window count had no corresponding timestamp.");
            transaction.Rollback();
            return new StaffInvitationIssueResult(
                StaffInvitationIssueOutcome.RateLimited,
                RetryAfterSeconds: GetRetryAfterSeconds(
                    oldestResendAt.AddMinutes(resendWindowMinutes), databaseTime));
        }

        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE auth.staff_invitations
            SET revoked_at = @DatabaseTime,
                updated_at = @DatabaseTime,
                updated_by = @ActorId
            WHERE user_id = @UserId AND purpose = 'STAFF_ACTIVATION'
              AND used_at IS NULL AND revoked_at IS NULL;
            INSERT INTO auth.staff_invitations
                (id, user_id, purpose, token_hash, expires_at, link_version, issuance_type,
                 created_at, created_by, updated_at, updated_by)
            VALUES
                (@InvitationId, @UserId, 'STAFF_ACTIVATION', @TokenHash, @ExpiresAt, 2, 'RESEND',
                 @DatabaseTime, @ActorId, @DatabaseTime, @ActorId);
            """, new
        {
            InvitationId = invitationId,
            UserId = userId,
            ActorId = actorId,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt,
            DatabaseTime = databaseTime
        },
            transaction, cancellationToken: cancellationToken));
        transaction.Commit();
        return new StaffInvitationIssueResult(StaffInvitationIssueOutcome.Issued, invitation);
    }

    private static int GetRetryAfterSeconds(DateTimeOffset availableAt, DateTimeOffset databaseTime) =>
        Math.Max(1, (int)Math.Ceiling((availableAt - databaseTime).TotalSeconds));

    private sealed record ResendWindow(int ResendCount, DateTimeOffset? OldestResendAt);

    public async Task<bool> ActivateAsync(
        string tokenHash,
        string passwordHash,
        short linkVersion,
        string? normalizedEmail,
        CancellationToken cancellationToken)
    {
        using var connection = sqlConnectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        var userId = await connection.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition("""
            SELECT user_id
            FROM auth.staff_invitations
            WHERE token_hash = @TokenHash
              AND purpose = 'STAFF_ACTIVATION'
              AND link_version = @LinkVersion
              AND used_at IS NULL
              AND revoked_at IS NULL;
            """, new { TokenHash = tokenHash, LinkVersion = linkVersion },
            transaction, cancellationToken: cancellationToken));

        if (userId is null)
        {
            transaction.Rollback();
            return false;
        }

        var lockedUserId = await connection.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition("""
            SELECT id
            FROM auth.users
            WHERE id = @UserId AND deleted_at IS NULL
            FOR UPDATE;
            """, new { UserId = userId.Value }, transaction, cancellationToken: cancellationToken));
        if (lockedUserId is null)
        {
            transaction.Rollback();
            return false;
        }

        var invitationId = await connection.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition("""
            SELECT id
            FROM auth.staff_invitations
            WHERE token_hash = @TokenHash
              AND user_id = @UserId
              AND purpose = 'STAFF_ACTIVATION'
              AND link_version = @LinkVersion
              AND used_at IS NULL
              AND revoked_at IS NULL
            FOR UPDATE;
            """, new { TokenHash = tokenHash, UserId = userId.Value, LinkVersion = linkVersion },
            transaction, cancellationToken: cancellationToken));
        if (invitationId is null)
        {
            transaction.Rollback();
            return false;
        }

        var databaseTime = await connection.QuerySingleAsync<DateTime>(new CommandDefinition(
            "SELECT clock_timestamp();",
            transaction: transaction,
            cancellationToken: cancellationToken));
        var isEligible = await connection.QuerySingleAsync<bool>(new CommandDefinition("""
            SELECT EXISTS (
                SELECT 1
                FROM auth.staff_invitations invitation
                JOIN auth.users user_row ON user_row.id = invitation.user_id
                WHERE invitation.id = @InvitationId
                  AND invitation.user_id = @UserId
                  AND invitation.token_hash = @TokenHash
                  AND invitation.purpose = 'STAFF_ACTIVATION'
                  AND invitation.link_version = @LinkVersion
                  AND invitation.used_at IS NULL
                  AND invitation.revoked_at IS NULL
                  AND invitation.expires_at > @DatabaseTime
                  AND (@NormalizedEmail IS NULL OR lower(user_row.email) = @NormalizedEmail)
                  AND NOT user_row.is_active
                  AND NOT user_row.is_email_verified
                  AND user_row.deleted_at IS NULL
                  AND EXISTS (
                      SELECT 1 FROM auth.branch_user_roles assignment
                      JOIN auth.roles role_row ON role_row.id = assignment.role_id
                      JOIN core.branches branch_row ON branch_row.id = assignment.branch_id
                      WHERE assignment.user_id = user_row.id
                        AND assignment.is_active AND assignment.removed_at IS NULL
                        AND role_row.code = 'STAFF' AND role_row.is_active
                        AND role_row.deleted_at IS NULL
                        AND branch_row.is_active AND branch_row.deleted_at IS NULL
                  )
                  AND NOT EXISTS (
                      SELECT 1 FROM auth.user_roles user_role
                      JOIN auth.roles role_row ON role_row.id = user_role.role_id
                      WHERE user_role.user_id = user_row.id
                        AND role_row.is_active AND role_row.deleted_at IS NULL
                        AND role_row.code <> 'STAFF'
                  )
                  AND NOT EXISTS (
                      SELECT 1 FROM auth.branch_user_roles assignment
                      JOIN auth.roles role_row ON role_row.id = assignment.role_id
                      WHERE assignment.user_id = user_row.id
                        AND assignment.is_active
                        AND assignment.removed_at IS NULL
                        AND role_row.is_active AND role_row.deleted_at IS NULL
                        AND role_row.code <> 'STAFF'
                  )
            );
            """, new
        {
            InvitationId = invitationId.Value,
            UserId = userId.Value,
            TokenHash = tokenHash,
            LinkVersion = linkVersion,
            NormalizedEmail = normalizedEmail,
            DatabaseTime = databaseTime
        }, transaction, cancellationToken: cancellationToken));

        if (!isEligible)
        {
            transaction.Rollback();
            return false;
        }

        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE auth.users SET password_hash = @PasswordHash, is_active = true,
                is_email_verified = true, updated_at = @DatabaseTime, updated_by = id
            WHERE id = @UserId;
            UPDATE auth.staff_invitations
            SET used_at = @DatabaseTime, updated_at = @DatabaseTime, updated_by = @UserId
            WHERE id = @InvitationId;
            UPDATE auth.staff_invitations
            SET revoked_at = @DatabaseTime, updated_at = @DatabaseTime, updated_by = @UserId
            WHERE user_id = @UserId
              AND purpose = 'STAFF_ACTIVATION'
              AND id <> @InvitationId
              AND used_at IS NULL
              AND revoked_at IS NULL;
            """, new
        {
            UserId = userId.Value,
            InvitationId = invitationId.Value,
            PasswordHash = passwordHash,
            DatabaseTime = databaseTime
        },
            transaction, cancellationToken: cancellationToken));
        transaction.Commit();
        return true;
    }

    public async Task<bool> SetInvitationDeliveryAsync(
        Guid invitationId, Guid actorId, bool sent, CancellationToken cancellationToken)
    {
        int updated;
        try
        {
            using var connection = sqlConnectionFactory.CreateConnection();
            updated = await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE auth.staff_invitations
                SET delivery_status = @Status,
                    delivery_error_code = @ErrorCode,
                    sent_at = CASE WHEN @Sent THEN clock_timestamp() ELSE NULL END,
                    updated_at = clock_timestamp(),
                    updated_by = @ActorId
                WHERE id = @InvitationId;
                """, new
            {
                InvitationId = invitationId,
                ActorId = actorId,
                Sent = sent,
                Status = sent ? "SENT" : "FAILED",
                ErrorCode = sent ? null : "INVITATION_SEND_FAILED"
            }, cancellationToken: cancellationToken));
        }
        catch (NpgsqlException)
        {
            logger.LogWarning(
                "Staff invitation delivery status update failed. InvitationId={InvitationId}",
                invitationId);
            return false;
        }
        catch (TimeoutException)
        {
            logger.LogWarning(
                "Staff invitation delivery status update timed out. InvitationId={InvitationId}",
                invitationId);
            return false;
        }

        if (updated != 1)
        {
            logger.LogWarning(
                "Staff invitation delivery status update affected an unexpected row count. InvitationId={InvitationId}",
                invitationId);
            return false;
        }

        return true;
    }

    public async Task<StaffCreationOutcome> CreateAsync(
        StaffCreation creation,
        CancellationToken cancellationToken)
    {
        using var connection = sqlConnectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        try
        {
            var branches = (await connection.QueryAsync<Guid>(new CommandDefinition("""
                SELECT b.id
                FROM core.branches b
                JOIN core.owners o ON o.id = b.owner_id
                JOIN auth.users actor ON actor.id = o.user_id
                WHERE b.id = ANY(@BranchIds)
                  AND o.id = @OwnerId AND o.user_id = @ActorId
                  AND o.is_active AND o.deleted_at IS NULL
                  AND actor.is_active AND actor.deleted_at IS NULL
                  AND b.is_active AND b.deleted_at IS NULL
                  AND EXISTS (
                      SELECT 1 FROM auth.roles role
                      JOIN auth.role_permissions rp ON rp.role_id = role.id
                      JOIN auth.permissions p ON p.id = rp.permission_id
                      WHERE role.code = 'BRANCH_OWNER' AND role.is_active
                        AND role.deleted_at IS NULL AND p.code = 'staff.create'
                  )
                ORDER BY b.id
                FOR UPDATE OF b, o, actor;
                """, new
            {
                creation.Request.BranchIds,
                creation.OwnerId,
                creation.ActorId
            }, transaction, cancellationToken: cancellationToken))).AsList();

            if (branches.Count != creation.Request.BranchIds.Length)
            {
                transaction.Rollback();
                return StaffCreationOutcome.BranchAccessDenied;
            }

            var roleId = await connection.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition("""
                SELECT id FROM auth.roles
                WHERE code = 'STAFF' AND is_active AND deleted_at IS NULL
                FOR SHARE;
                """, transaction: transaction, cancellationToken: cancellationToken));
            if (roleId is null)
            {
                transaction.Rollback();
                return StaffCreationOutcome.StaffRoleUnavailable;
            }

            var exists = await connection.ExecuteScalarAsync<bool>(new CommandDefinition("""
                SELECT EXISTS (
                    SELECT 1 FROM auth.users
                    WHERE deleted_at IS NULL
                      AND (lower(email) = @Email OR phone_number = @PhoneNumber)
                );
                """, creation.Request, transaction, cancellationToken: cancellationToken));
            if (exists)
            {
                transaction.Rollback();
                return StaffCreationOutcome.UserAlreadyExists;
            }

            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO auth.users
                    (id, email, full_name, phone_number, password_hash, is_active, is_email_verified,
                     created_at, created_by, updated_at, updated_by)
                VALUES
                    (@UserId, @Email, @DisplayName, @PhoneNumber, @PasswordHash, false, false,
                     now(), @ActorId, now(), @ActorId);
                INSERT INTO auth.branch_user_roles
                    (branch_id, user_id, role_id, is_active, staff_code, job_title,
                     employment_start_date, notes, created_at, created_by, updated_at, updated_by)
                SELECT branch_id, @UserId, @RoleId, true, @StaffCode, @JobTitle,
                       @EmploymentStartDate, @Notes, now(), @ActorId, now(), @ActorId
                FROM unnest(@BranchIds::uuid[]) AS branch_ids(branch_id);
                INSERT INTO auth.user_roles
                (
                    user_id,
                    role_id,
                    assigned_at,
                    assigned_by
                )
                VALUES
                (
                    @UserId,
                    @RoleId,
                    now(),
                    @ActorId
                );
                INSERT INTO auth.staff_invitations
                    (id, user_id, purpose, token_hash, expires_at, link_version, issuance_type, created_by, updated_by)
                VALUES
                    (@InvitationId, @UserId, 'STAFF_ACTIVATION', @TokenHash, @ExpiresAt, 2, 'CREATE', @ActorId, @ActorId);
                """, new
            {
                creation.UserId,
                creation.InvitationId,
                creation.ActorId,
                creation.PasswordHash,
                creation.TokenHash,
                creation.ExpiresAt,
                creation.Request.Email,
                creation.Request.DisplayName,
                creation.Request.PhoneNumber,
                creation.Request.StaffCode,
                creation.Request.JobTitle,
                creation.Request.EmploymentStartDate,
                creation.Request.Notes,
                creation.Request.BranchIds,
                RoleId = roleId.Value
            }, transaction, cancellationToken: cancellationToken));
            transaction.Commit();
            return StaffCreationOutcome.Created;
        }
        catch (PostgresException exception) when (
            exception.SqlState == PostgresErrorCodes.UniqueViolation &&
            exception.ConstraintName is "ux_users_email" or "ux_users_normalized_email" or
                "ux_users_active_phone_number" or "ux_branch_user_roles_active_staff_code")
        {
            transaction.Rollback();
            return exception.ConstraintName == "ux_branch_user_roles_active_staff_code"
                ? StaffCreationOutcome.StaffCodeAlreadyUsed
                : StaffCreationOutcome.UserAlreadyExists;
        }
    }

    private const string EligibleStaffCte = """
        WITH eligible_staff AS (
            SELECT
                u.id AS "UserId",
                COALESCE(u.full_name, '') AS "DisplayName",
                u.email AS "Email",
                u.phone_number AS "PhoneNumber",
                (
                    SELECT MIN(assignment.staff_code)
                    FROM auth.branch_user_roles assignment
                    JOIN core.branches assignment_branch
                      ON assignment_branch.id = assignment.branch_id
                    JOIN auth.roles assignment_role
                      ON assignment_role.id = assignment.role_id
                    WHERE assignment.user_id = u.id
                      AND assignment.is_active
                      AND assignment.removed_at IS NULL
                      AND assignment_role.code = 'STAFF'
                      AND assignment_role.is_active
                      AND assignment_role.deleted_at IS NULL
                      AND assignment_branch.owner_id = @OwnerId
                      AND assignment_branch.id = ANY(@BranchIds)
                      AND assignment_branch.is_active
                      AND assignment_branch.deleted_at IS NULL
                ) AS "StaffCode",
                CASE WHEN u.is_active THEN 'ACTIVE' ELSE 'INACTIVE' END AS "Status",
                CASE
                    WHEN u.is_active THEN 'ACTIVE'
                    WHEN EXISTS (
                        SELECT 1
                        FROM auth.staff_invitations invitation
                        WHERE invitation.user_id = u.id
                          AND invitation.purpose = 'STAFF_ACTIVATION'
                          AND invitation.used_at IS NULL
                          AND invitation.revoked_at IS NULL
                          AND invitation.expires_at > now()
                    ) THEN 'PENDING_ACTIVATION'
                    WHEN EXISTS (
                        SELECT 1
                        FROM auth.staff_invitations invitation
                        WHERE invitation.user_id = u.id
                          AND invitation.purpose = 'STAFF_ACTIVATION'
                          AND invitation.used_at IS NULL
                          AND invitation.revoked_at IS NULL
                          AND invitation.expires_at <= now()
                    ) THEN 'EXPIRED'
                    ELSE 'INACTIVE'
                END AS "ActivationStatus",
                CASE WHEN u.is_active THEN NULL ELSE (
                    SELECT invitation.delivery_status
                    FROM auth.staff_invitations invitation
                    WHERE invitation.user_id = u.id
                      AND invitation.purpose = 'STAFF_ACTIVATION'
                      AND invitation.used_at IS NULL
                      AND invitation.revoked_at IS NULL
                    ORDER BY invitation.created_at DESC, invitation.id DESC
                    LIMIT 1
                ) END AS "InvitationDeliveryStatus",
                u.last_login_at AS "LastLoginAt",
                u.created_at AS "CreatedAt"
            FROM auth.users u
            WHERE (CAST(@UserId AS uuid) IS NULL OR u.id = @UserId)
              AND u.deleted_at IS NULL
              AND EXISTS (
                  SELECT 1
                  FROM auth.branch_user_roles assignment
                  JOIN auth.roles assignment_role
                    ON assignment_role.id = assignment.role_id
                  JOIN core.branches assignment_branch
                    ON assignment_branch.id = assignment.branch_id
                  WHERE assignment.user_id = u.id
                    AND assignment.is_active
                    AND assignment.removed_at IS NULL
                    AND assignment_role.code = 'STAFF'
                    AND assignment_role.is_active
                    AND assignment_role.deleted_at IS NULL
                    AND assignment_branch.owner_id = @OwnerId
                    AND assignment_branch.id = ANY(@BranchIds)
                    AND assignment_branch.is_active
                    AND assignment_branch.deleted_at IS NULL
              )
              AND NOT EXISTS (
                  SELECT 1
                  FROM auth.user_roles user_role
                  JOIN auth.roles protected_role
                    ON protected_role.id = user_role.role_id
                  WHERE user_role.user_id = u.id
                    AND protected_role.code = ANY(@ProtectedRoleCodes)
                    AND protected_role.is_active
                    AND protected_role.deleted_at IS NULL
              )
              AND NOT EXISTS (
                  SELECT 1
                  FROM auth.branch_user_roles branch_role
                  JOIN auth.roles protected_role
                    ON protected_role.id = branch_role.role_id
                  WHERE branch_role.user_id = u.id
                    AND branch_role.is_active
                    AND branch_role.removed_at IS NULL
                    AND protected_role.code = ANY(@ProtectedRoleCodes)
                    AND protected_role.is_active
                    AND protected_role.deleted_at IS NULL
              )
              AND (
                  CAST(@SearchPattern AS text) IS NULL
                  OR COALESCE(u.full_name, '') ILIKE @SearchPattern
                  OR u.email ILIKE @SearchPattern
                  OR COALESCE(u.phone_number, '') ILIKE @SearchPattern
                  OR EXISTS (
                      SELECT 1
                      FROM auth.branch_user_roles assignment
                      JOIN auth.roles assignment_role
                        ON assignment_role.id = assignment.role_id
                      JOIN core.branches assignment_branch
                        ON assignment_branch.id = assignment.branch_id
                      WHERE assignment.user_id = u.id
                        AND assignment.is_active
                        AND assignment.removed_at IS NULL
                        AND assignment_role.code = 'STAFF'
                        AND assignment_role.is_active
                        AND assignment_role.deleted_at IS NULL
                        AND assignment_branch.owner_id = @OwnerId
                        AND assignment_branch.id = ANY(@BranchIds)
                        AND assignment_branch.is_active
                        AND assignment_branch.deleted_at IS NULL
                        AND assignment.staff_code ILIKE @SearchPattern
                  )
              )
              AND (
                  CAST(@Status AS text) IS NULL
                  OR (CASE WHEN u.is_active THEN 'ACTIVE' ELSE 'INACTIVE' END) = @Status
              )
              AND (
                  CAST(@ActivationStatus AS text) IS NULL
                  OR (
                      CASE
                          WHEN u.is_active THEN 'ACTIVE'
                          WHEN EXISTS (
                              SELECT 1
                              FROM auth.staff_invitations invitation
                              WHERE invitation.user_id = u.id
                                AND invitation.purpose = 'STAFF_ACTIVATION'
                                AND invitation.used_at IS NULL
                                AND invitation.revoked_at IS NULL
                                AND invitation.expires_at > now()
                          ) THEN 'PENDING_ACTIVATION'
                          WHEN EXISTS (
                              SELECT 1
                              FROM auth.staff_invitations invitation
                              WHERE invitation.user_id = u.id
                                AND invitation.purpose = 'STAFF_ACTIVATION'
                                AND invitation.used_at IS NULL
                                AND invitation.revoked_at IS NULL
                                AND invitation.expires_at <= now()
                          ) THEN 'EXPIRED'
                          ELSE 'INACTIVE'
                      END
                  ) = @ActivationStatus
              )
        )
        """;

    private static readonly string[] ProtectedRoleCodes =
        ["ADMIN", "SUPER_ADMIN", "BRANCH_ADMIN", "BRANCH_OWNER", "CUSTOMER", "BRANCH_MANAGER"];

    public async Task<StaffOwnerScope?> GetOwnerScopeAsync(
        Guid ownerUserId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                owner.id AS "OwnerId",
                branch.id AS "BranchId",
                branch.name AS "BranchName"
            FROM core.owners owner
            LEFT JOIN core.branches branch
              ON branch.owner_id = owner.id
             AND branch.is_active
             AND branch.deleted_at IS NULL
            WHERE owner.user_id = @OwnerUserId
              AND owner.is_active
              AND owner.deleted_at IS NULL
            ORDER BY branch.name NULLS LAST, branch.id;
            """;

        using var connection = sqlConnectionFactory.CreateConnection();
        var rows = (await connection.QueryAsync<OwnerScopeRow>(new CommandDefinition(
            sql,
            new { OwnerUserId = ownerUserId },
            cancellationToken: cancellationToken))).AsList();

        if (rows.Count == 0)
        {
            return null;
        }

        var ownerId = rows[0].OwnerId;
        var branches = rows
            .Where(row => row.BranchId is not null && row.BranchName is not null)
            .Select(row => new StaffOwnerBranch(row.BranchId!.Value, row.BranchName!))
            .ToArray();

        return new StaffOwnerScope(ownerId, branches);
    }

    public async Task<PagedResult<StaffListItemResponse>> SearchAsync(
        Guid ownerId,
        IReadOnlyCollection<Guid> branchIds,
        StaffListRequest request,
        CancellationToken cancellationToken)
    {
        var sortColumn = request.SortBy switch
        {
            "displayName" => "staff.\"DisplayName\"",
            "email" => "staff.\"Email\"",
            "staffCode" => "staff.\"StaffCode\"",
            "status" => "staff.\"Status\"",
            "lastLoginAt" => "staff.\"LastLoginAt\"",
            _ => "staff.\"CreatedAt\""
        };
        var sortDirection = request.SortDirection == "ASC" ? "ASC" : "DESC";
        var sql = $"""
            {EligibleStaffCte}
            SELECT COUNT(*)::bigint
            FROM eligible_staff;

            {EligibleStaffCte}
            SELECT
                staff."UserId",
                staff."DisplayName",
                staff."Email",
                staff."PhoneNumber",
                staff."StaffCode",
                staff."Status",
                staff."ActivationStatus",
                staff."InvitationDeliveryStatus",
                staff."LastLoginAt",
                staff."CreatedAt"
            FROM eligible_staff staff
            ORDER BY {sortColumn} {sortDirection} NULLS LAST, staff."UserId" ASC
            LIMIT @PageSize
            OFFSET @Offset;
            """;

        const string branchesSql = """
            SELECT
                assignment.user_id AS "UserId",
                branch.id AS "BranchId",
                branch.name AS "BranchName",
                'ACTIVE' AS "AssignmentStatus",
                assignment.staff_code AS "StaffCode",
                assignment.job_title AS "JobTitle",
                assignment.employment_start_date AS "EmploymentStartDate",
                assignment.notes AS "Notes"
            FROM auth.branch_user_roles assignment
            JOIN auth.roles assigned_role
              ON assigned_role.id = assignment.role_id
            JOIN core.branches branch
              ON branch.id = assignment.branch_id
            WHERE assignment.user_id = ANY(@UserIds)
              AND assignment.branch_id = ANY(@BranchIds)
              AND assignment.is_active
              AND assignment.removed_at IS NULL
              AND assigned_role.code = 'STAFF'
              AND assigned_role.is_active
              AND assigned_role.deleted_at IS NULL
              AND branch.owner_id = @OwnerId
              AND branch.is_active
              AND branch.deleted_at IS NULL
            ORDER BY branch.name, assignment.user_id;
            """;

        var parameters = CreateParameters(ownerId, branchIds, request);
        parameters.Add("PageSize", request.PageSize);
        parameters.Add("Offset", ((long)request.Page - 1) * request.PageSize);

        using var connection = sqlConnectionFactory.CreateConnection();
        long totalCount;
        List<StaffListRow> staffRows;
        using (var results = await connection.QueryMultipleAsync(new CommandDefinition(
            sql,
            parameters,
            cancellationToken: cancellationToken)))
        {
            totalCount = await results.ReadSingleAsync<long>();
            staffRows = (await results.ReadAsync<StaffListRow>()).AsList();
        }

        var userIds = staffRows.Select(row => row.UserId).ToArray();
        var branchRows = await connection.QueryAsync<StaffBranchRow>(new CommandDefinition(
            branchesSql,
            new { UserIds = userIds, BranchIds = branchIds.ToArray(), OwnerId = ownerId },
            cancellationToken: cancellationToken));
        var branchesByUser = branchRows
            .GroupBy(row => row.UserId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<StaffBranchResponse>)group
                    .Select(ToBranchResponse)
                    .ToArray());

        var items = staffRows
            .Select(row => new StaffListItemResponse(
                row.UserId,
                row.DisplayName,
                row.Email,
                row.PhoneNumber,
                row.StaffCode,
                row.Status,
                row.ActivationStatus,
                row.InvitationDeliveryStatus,
                branchesByUser.GetValueOrDefault(row.UserId, Array.Empty<StaffBranchResponse>()),
                row.LastLoginAt,
                row.CreatedAt))
            .ToArray();

        return PagedResult<StaffListItemResponse>.Create(items, request.Page, request.PageSize, totalCount);
    }

    public async Task<StaffDetailResponse?> GetByIdAsync(
        Guid ownerId,
        Guid userId,
        IReadOnlyCollection<Guid> branchIds,
        CancellationToken cancellationToken)
    {
        var sql = $"""
            {EligibleStaffCte}
            SELECT
                staff."UserId",
                staff."DisplayName",
                staff."Email",
                staff."PhoneNumber",
                staff."Status",
                staff."ActivationStatus",
                staff."InvitationDeliveryStatus",
                staff."LastLoginAt",
                staff."CreatedAt"
            FROM eligible_staff staff;

            SELECT
                assignment.user_id AS "UserId",
                branch.id AS "BranchId",
                branch.name AS "BranchName",
                'ACTIVE' AS "AssignmentStatus",
                assignment.staff_code AS "StaffCode",
                assignment.job_title AS "JobTitle",
                assignment.employment_start_date AS "EmploymentStartDate",
                assignment.notes AS "Notes"
            FROM auth.branch_user_roles assignment
            JOIN auth.roles assigned_role
              ON assigned_role.id = assignment.role_id
            JOIN core.branches branch
              ON branch.id = assignment.branch_id
            WHERE assignment.user_id = @UserId
              AND assignment.branch_id = ANY(@BranchIds)
              AND assignment.is_active
              AND assignment.removed_at IS NULL
              AND assigned_role.code = 'STAFF'
              AND assigned_role.is_active
              AND assigned_role.deleted_at IS NULL
              AND branch.owner_id = @OwnerId
              AND branch.is_active
              AND branch.deleted_at IS NULL
            ORDER BY branch.name, assignment.user_id;
            """;

        var parameters = CreateParameters(ownerId, branchIds, request: null);
        parameters.Add("UserId", userId);

        using var connection = sqlConnectionFactory.CreateConnection();
        using var results = await connection.QueryMultipleAsync(new CommandDefinition(
            sql,
            parameters,
            cancellationToken: cancellationToken));

        var staff = await results.ReadSingleOrDefaultAsync<StaffDetailRow>();
        var branches = (await results.ReadAsync<StaffBranchRow>())
            .Select(ToBranchResponse)
            .ToArray();

        return staff is null
            ? null
            : new StaffDetailResponse(
                staff.UserId,
                staff.DisplayName,
                staff.Email,
                staff.PhoneNumber,
                staff.Status,
                staff.ActivationStatus,
                staff.InvitationDeliveryStatus,
                branches,
                staff.LastLoginAt,
                staff.CreatedAt);
    }

    private static DynamicParameters CreateParameters(
        Guid ownerId,
        IReadOnlyCollection<Guid> branchIds,
        StaffListRequest? request)
    {
        var parameters = new DynamicParameters();
        parameters.Add("OwnerId", ownerId);
        parameters.Add("BranchIds", branchIds.ToArray());
        parameters.Add("UserId", null);
        parameters.Add(
            "SearchPattern",
            string.IsNullOrWhiteSpace(request?.Search) ? null : $"%{request.Search.Trim()}%");
        parameters.Add("Status", request?.Status);
        parameters.Add("ActivationStatus", request?.ActivationStatus);
        parameters.Add("ProtectedRoleCodes", ProtectedRoleCodes);
        return parameters;
    }

    private static StaffBranchResponse ToBranchResponse(StaffBranchRow row) =>
        new(
            row.BranchId,
            row.BranchName,
            row.AssignmentStatus,
            row.StaffCode,
            row.JobTitle,
            row.EmploymentStartDate is DateTime date ? DateOnly.FromDateTime(date) : null,
            row.Notes);

    private sealed class OwnerScopeRow
    {
        public Guid OwnerId { get; init; }
        public Guid? BranchId { get; init; }
        public string? BranchName { get; init; }
    }

    private sealed class StaffListRow
    {
        public Guid UserId { get; init; }
        public string DisplayName { get; init; } = string.Empty;
        public string Email { get; init; } = string.Empty;
        public string? PhoneNumber { get; init; }
        public string? StaffCode { get; init; }
        public string Status { get; init; } = string.Empty;
        public string ActivationStatus { get; init; } = string.Empty;
        public string? InvitationDeliveryStatus { get; init; }
        public DateTimeOffset? LastLoginAt { get; init; }
        public DateTimeOffset CreatedAt { get; init; }
    }

    private sealed class StaffDetailRow
    {
        public Guid UserId { get; init; }
        public string DisplayName { get; init; } = string.Empty;
        public string Email { get; init; } = string.Empty;
        public string? PhoneNumber { get; init; }
        public string Status { get; init; } = string.Empty;
        public string ActivationStatus { get; init; } = string.Empty;
        public string? InvitationDeliveryStatus { get; init; }
        public DateTimeOffset? LastLoginAt { get; init; }
        public DateTimeOffset CreatedAt { get; init; }
    }

    private sealed class StaffBranchRow
    {
        public Guid UserId { get; init; }
        public Guid BranchId { get; init; }
        public string BranchName { get; init; } = string.Empty;
        public string AssignmentStatus { get; init; } = string.Empty;
        public string? StaffCode { get; init; }
        public string? JobTitle { get; init; }
        public DateTime? EmploymentStartDate { get; init; }
        public string? Notes { get; init; }
    }
}
