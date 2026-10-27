using System.Data;
using System.Text;
using CourtBookingManagement.Application.Abstractions.Data;
using CourtBookingManagement.Application.Matching.Models;
using CourtBookingManagement.Application.Owners.Models;
using CourtBookingManagement.Application.Owners.Repositories;
using Dapper;
using Npgsql;

namespace CourtBookingManagement.Infrastructure.Repositories;

public sealed class OwnerRepository(ISqlConnectionFactory sqlConnectionFactory) : IOwnerRepository
{
    private const string SelectColumns = """
        o.id AS Id,
        o.user_id AS UserId,
        u.full_name AS FullName,
        u.email AS Email,
        u.phone_number AS PhoneNumber,
        o.business_name AS BusinessName,
        o.tax_id AS TaxId,
        o.is_active AS IsActive,
        o.created_at AS CreatedAt,
        o.updated_at AS UpdatedAt
        """;

    public async Task<PagedResult<OwnerResponse>> SearchAsync(
        OwnerSearchRequest request,
        CancellationToken cancellationToken)
    {
        using var connection = sqlConnectionFactory.CreateConnection();

        var where = new StringBuilder("WHERE o.deleted_at IS NULL");
        var parameters = new DynamicParameters();
        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            where.Append(" AND (o.business_name ILIKE @Keyword ESCAPE '\\' OR o.tax_id ILIKE @Keyword ESCAPE '\\'"
                + " OR u.email ILIKE @Keyword ESCAPE '\\' OR u.full_name ILIKE @Keyword ESCAPE '\\'"
                + " OR u.phone_number ILIKE @Keyword ESCAPE '\\')");
            parameters.Add("Keyword", $"%{EscapeLike(request.Keyword)}%");
        }

        if (request.IsActive.HasValue)
        {
            where.Append(" AND o.is_active = @IsActive");
            parameters.Add("IsActive", request.IsActive.Value);
        }

        var orderBy = request.SortBy switch
        {
            "oldest" => "ORDER BY o.created_at ASC, o.id ASC",
            "name_asc" => "ORDER BY o.business_name ASC, o.id ASC",
            "name_desc" => "ORDER BY o.business_name DESC, o.id DESC",
            _ => "ORDER BY o.created_at DESC, o.id DESC"
        };

        var from = "FROM core.owners o JOIN auth.users u ON u.id = o.user_id";

        var totalCount = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            $"SELECT COUNT(*) {from} {where};", parameters, cancellationToken: cancellationToken));

        parameters.Add("PageSize", request.PageSize);
        parameters.Add("Offset", ((long)request.PageNumber - 1) * request.PageSize);

        var items = (await connection.QueryAsync<OwnerResponse>(new CommandDefinition(
            $"SELECT {SelectColumns} {from} {where} {orderBy} LIMIT @PageSize OFFSET @Offset;",
            parameters,
            cancellationToken: cancellationToken))).AsList();

        return PagedResult<OwnerResponse>.Create(items, request.PageNumber, request.PageSize, totalCount);
    }

    public async Task<OwnerResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var sql = $"""
            SELECT {SelectColumns}
            FROM core.owners o
            JOIN auth.users u ON u.id = o.user_id
            WHERE o.id = @Id AND o.deleted_at IS NULL;
            """;

        using var connection = sqlConnectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<OwnerResponse>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<(OwnerWriteStatus Status, Guid Id)> CreateAsync(
        CreateOwnerRequest request,
        Guid? actorId,
        CancellationToken cancellationToken)
    {
        using var connection = sqlConnectionFactory.CreateConnection();
        EnsureOpen(connection);
        using var transaction = connection.BeginTransaction();

        // Locking the user row serializes concurrent creates for the same user.
        var userExists = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            """
            SELECT EXISTS (
                SELECT 1 FROM auth.users WHERE id = @UserId AND deleted_at IS NULL AND is_active = TRUE FOR UPDATE);
            """,
            new { request.UserId },
            transaction,
            cancellationToken: cancellationToken));
        if (!userExists)
        {
            return (OwnerWriteStatus.UserNotFound, Guid.Empty);
        }

        var userHasOwner = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS (SELECT 1 FROM core.owners WHERE user_id = @UserId AND deleted_at IS NULL);",
            new { request.UserId },
            transaction,
            cancellationToken: cancellationToken));
        if (userHasOwner)
        {
            return (OwnerWriteStatus.UserAlreadyOwner, Guid.Empty);
        }

        if (request.TaxId is not null && await TaxIdExistsAsync(connection, transaction, request.TaxId, null, cancellationToken))
        {
            return (OwnerWriteStatus.TaxIdTaken, Guid.Empty);
        }

        try
        {
            var id = await connection.ExecuteScalarAsync<Guid>(new CommandDefinition(
                """
                INSERT INTO core.owners
                    (user_id, business_name, tax_id, is_active, created_at, created_by, updated_at, updated_by)
                VALUES
                    (@UserId, @BusinessName, @TaxId, @IsActive, NOW(), @ActorId, NOW(), @ActorId)
                RETURNING id;
                """,
                new { request.UserId, request.BusinessName, request.TaxId, request.IsActive, ActorId = actorId },
                transaction,
                cancellationToken: cancellationToken));

            transaction.Commit();
            return (OwnerWriteStatus.Success, id);
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return (exception.ConstraintName?.Contains("tax", StringComparison.OrdinalIgnoreCase) == true
                ? OwnerWriteStatus.TaxIdTaken
                : OwnerWriteStatus.UserAlreadyOwner, Guid.Empty);
        }
    }

    public async Task<OwnerWriteStatus> UpdateAsync(
        Guid id,
        UpdateOwnerRequest request,
        Guid? actorId,
        CancellationToken cancellationToken)
    {
        using var connection = sqlConnectionFactory.CreateConnection();
        EnsureOpen(connection);
        using var transaction = connection.BeginTransaction();

        var exists = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS (SELECT 1 FROM core.owners WHERE id = @Id AND deleted_at IS NULL FOR UPDATE);",
            new { Id = id },
            transaction,
            cancellationToken: cancellationToken));
        if (!exists)
        {
            return OwnerWriteStatus.NotFound;
        }

        if (request.TaxId is not null && await TaxIdExistsAsync(connection, transaction, request.TaxId, id, cancellationToken))
        {
            return OwnerWriteStatus.TaxIdTaken;
        }

        if (!request.IsActive && await HasActiveBranchesAsync(connection, transaction, id, cancellationToken))
        {
            return OwnerWriteStatus.HasActiveBranches;
        }

        try
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                UPDATE core.owners
                SET business_name = @BusinessName,
                    tax_id = @TaxId,
                    is_active = @IsActive,
                    updated_at = NOW(),
                    updated_by = @ActorId
                WHERE id = @Id AND deleted_at IS NULL;
                """,
                new { Id = id, request.BusinessName, request.TaxId, request.IsActive, ActorId = actorId },
                transaction,
                cancellationToken: cancellationToken));

            transaction.Commit();
            return OwnerWriteStatus.Success;
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return OwnerWriteStatus.TaxIdTaken;
        }
    }

    public async Task<OwnerWriteStatus> DeleteAsync(Guid id, Guid? actorId, CancellationToken cancellationToken)
    {
        using var connection = sqlConnectionFactory.CreateConnection();
        EnsureOpen(connection);
        using var transaction = connection.BeginTransaction();

        var exists = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS (SELECT 1 FROM core.owners WHERE id = @Id AND deleted_at IS NULL FOR UPDATE);",
            new { Id = id },
            transaction,
            cancellationToken: cancellationToken));
        if (!exists)
        {
            return OwnerWriteStatus.NotFound;
        }

        if (await HasActiveBranchesAsync(connection, transaction, id, cancellationToken))
        {
            return OwnerWriteStatus.HasActiveBranches;
        }

        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE core.owners
            SET deleted_at = NOW(),
                deleted_by = @ActorId,
                is_active = FALSE,
                updated_at = NOW(),
                updated_by = @ActorId
            WHERE id = @Id AND deleted_at IS NULL;
            """,
            new { Id = id, ActorId = actorId },
            transaction,
            cancellationToken: cancellationToken));

        transaction.Commit();
        return OwnerWriteStatus.Success;
    }

    private static Task<bool> TaxIdExistsAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        string taxId,
        Guid? excludeOwnerId,
        CancellationToken cancellationToken) =>
        connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            """
            SELECT EXISTS (
                SELECT 1 FROM core.owners
                WHERE deleted_at IS NULL
                  AND lower(tax_id) = lower(@TaxId)
                  AND (@ExcludeId::uuid IS NULL OR id <> @ExcludeId::uuid));
            """,
            new { TaxId = taxId, ExcludeId = excludeOwnerId },
            transaction,
            cancellationToken: cancellationToken));

    private static Task<bool> HasActiveBranchesAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        Guid ownerId,
        CancellationToken cancellationToken) =>
        connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS (SELECT 1 FROM core.branches WHERE owner_id = @OwnerId AND deleted_at IS NULL);",
            new { OwnerId = ownerId },
            transaction,
            cancellationToken: cancellationToken));

    private static void EnsureOpen(IDbConnection connection)
    {
        if (connection.State != ConnectionState.Open)
        {
            connection.Open();
        }
    }

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}
