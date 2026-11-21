using System.Data;
using CourtBookingManagement.Application.Abstractions.Data;
using CourtBookingManagement.Application.Profiles.Models;
using CourtBookingManagement.Application.Profiles.Repositories;
using Dapper;

namespace CourtBookingManagement.Infrastructure.Repositories;

public sealed class MyProfileRepository(ISqlConnectionFactory sqlConnectionFactory)
    : IMyProfileRepository
{
    private const string ProfileQuery = """
        SELECT
            u.id AS UserId,
            c.id AS CustomerId,
            u.full_name AS DisplayName,
            u.email AS Email,
            u.phone_number AS PhoneNumber,
            u.avatar_file_id AS AvatarFileId,
            f.secure_url AS AvatarUrl,
            f.file_name AS AvatarFileName,
            u.updated_at AS UpdatedAt
        FROM auth.users u
        LEFT JOIN customer.customers c
            ON c.user_id = u.id
           AND c.deleted_at IS NULL
        LEFT JOIN media.files f
            ON f.id = u.avatar_file_id
           AND f.deleted_at IS NULL
        WHERE u.id = @UserId
          AND u.deleted_at IS NULL
        """;

    public async Task<MyProfileRecord?> GetByUserIdAsync(
        Guid userId,
        IDbTransaction? transaction,
        CancellationToken cancellationToken)
    {
        var sql = transaction is null
            ? ProfileQuery
            : $"{ProfileQuery}\nFOR UPDATE OF u";
        var command = new CommandDefinition(
            sql,
            new { UserId = userId },
            transaction,
            cancellationToken: cancellationToken);

        if (transaction is not null)
        {
            return await transaction.Connection!.QuerySingleOrDefaultAsync<MyProfileRecord>(command);
        }

        using var connection = sqlConnectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<MyProfileRecord>(command);
    }

    public async Task UpdateAsync(
        Guid userId,
        string displayName,
        Guid? previousAvatarFileId,
        Guid? newAvatarFileId,
        IDbTransaction transaction,
        CancellationToken cancellationToken)
    {
        const string updateUserSql = """
            UPDATE auth.users
            SET full_name = @DisplayName,
                avatar_file_id = @NewAvatarFileId,
                updated_at = NOW(),
                updated_by = @UserId
            WHERE id = @UserId
              AND deleted_at IS NULL;
            """;

        var connection = transaction.Connection
            ?? throw new InvalidOperationException("The profile transaction has no database connection.");
        var userRows = await connection.ExecuteAsync(new CommandDefinition(
            updateUserSql,
            new { UserId = userId, DisplayName = displayName, NewAvatarFileId = newAvatarFileId },
            transaction,
            cancellationToken: cancellationToken));
        if (userRows != 1)
        {
            throw new InvalidOperationException("The current user could not be updated.");
        }

        const string updateCustomerSql = """
            UPDATE customer.customers
            SET full_name = @DisplayName,
                updated_at = NOW(),
                updated_by = @UserId
            WHERE user_id = @UserId
              AND deleted_at IS NULL;
            """;
        await connection.ExecuteAsync(new CommandDefinition(
            updateCustomerSql,
            new { UserId = userId, DisplayName = displayName },
            transaction,
            cancellationToken: cancellationToken));

        if (newAvatarFileId == previousAvatarFileId)
        {
            return;
        }

        if (newAvatarFileId is Guid newFileId)
        {
            const string attachFileSql = """
                UPDATE media.files
                SET status = 'ATTACHED',
                    updated_at = NOW(),
                    updated_by = @UserId
                WHERE id = @FileId
                  AND uploader_user_id = @UserId
                  AND category = 'avatar'
                  AND status = 'TEMPORARY'
                  AND deleted_at IS NULL;
                """;
            var attachedRows = await connection.ExecuteAsync(new CommandDefinition(
                attachFileSql,
                new { UserId = userId, FileId = newFileId },
                transaction,
                cancellationToken: cancellationToken));
            if (attachedRows != 1)
            {
                throw new InvalidOperationException(
                    "The avatar file changed eligibility during the profile update.");
            }
        }

        if (previousAvatarFileId is Guid previousFileId)
        {
            const string replaceFileSql = """
                UPDATE media.files
                SET status = 'REPLACED',
                    updated_at = NOW(),
                    updated_by = @UserId
                WHERE id = @FileId
                  AND uploader_user_id = @UserId
                  AND status = 'ATTACHED';
                """;
            var replacedRows = await connection.ExecuteAsync(new CommandDefinition(
                replaceFileSql,
                new { UserId = userId, FileId = previousFileId },
                transaction,
                cancellationToken: cancellationToken));
            if (replacedRows != 1)
            {
                throw new InvalidOperationException(
                    "The previous avatar file state is inconsistent with the profile.");
            }
        }
    }
}
