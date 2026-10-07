using System.Text;
using CourtBookingManagement.Application.Abstractions.Data;
using CourtBookingManagement.Application.Courts.Models.Requests;
using CourtBookingManagement.Application.Courts.Models.Responses;
using CourtBookingManagement.Application.Courts.Repositories;
using CourtBookingManagement.Application.Courts.Services;
using CourtBookingManagement.Application.Matching.Models;
using Dapper;
using Npgsql;

namespace CourtBookingManagement.Infrastructure.Repositories;

public sealed class CourtRepository(ISqlConnectionFactory sqlConnectionFactory) : ICourtRepository
{
    private const string CourtNumberUniqueConstraint = "ux_courts_branch_number";

    public async Task<PagedResult<CourtListItemResponse>> SearchAsync(
        CourtSearchRequest request,
        CancellationToken cancellationToken)
    {
        var parameters = CreateParameters(request);
        var sql = new StringBuilder();

        AppendBaseQuery(sql);
        sql.AppendLine("SELECT * FROM court_data WHERE 1 = 1");
        AppendFilters(sql, request);
        sql.AppendLine(GetSortExpression(request.SortBy));
        sql.AppendLine("LIMIT @PageSize OFFSET @Offset;");
        parameters.Add("PageSize", request.PageSize);
        parameters.Add("Offset", ((long)request.PageNumber - 1) * request.PageSize);

        using var connection = sqlConnectionFactory.CreateConnection();
        var items = (await connection.QueryAsync<CourtListItemResponse>(
            new CommandDefinition(sql.ToString(), parameters, cancellationToken: cancellationToken))).AsList();

        var totalCount = await CountAsync(request, cancellationToken);

        return PagedResult<CourtListItemResponse>.Create(items, request.PageNumber, request.PageSize, totalCount);
    }

    public async Task<long> CountAsync(
        CourtSearchRequest request,
        CancellationToken cancellationToken)
    {
        var sql = new StringBuilder();

        AppendBaseQuery(sql);
        sql.AppendLine("SELECT COUNT(*) FROM court_data WHERE 1 = 1");
        AppendFilters(sql, request);

        using var connection = sqlConnectionFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<long>(
            new CommandDefinition(sql.ToString(), CreateParameters(request), cancellationToken: cancellationToken));
    }

    public async Task<CourtDetailResponse?> GetByIdAsync(
        Guid courtId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                c.id AS Id,
                c.branch_id AS BranchId,
                b.name AS BranchName,
                c.court_number AS CourtNumber,
                c.name AS Name,
                c.court_type_id AS CourtTypeId,
                ct.code AS CourtTypeCode,
                ct.name AS CourtTypeName,
                c.status AS Status,
                c.is_active AS IsActive,
                c.created_at AS CreatedAt,
                c.created_by AS CreatedBy,
                cu.full_name AS CreatedByName,
                c.updated_at AS UpdatedAt,
                c.updated_by AS UpdatedBy,
                uu.full_name AS UpdatedByName
            FROM core.courts c
            INNER JOIN core.branches b ON b.id = c.branch_id
            LEFT JOIN core.court_types ct ON ct.id = c.court_type_id
            LEFT JOIN auth.users cu ON cu.id = c.created_by
            LEFT JOIN auth.users uu ON uu.id = c.updated_by
            WHERE c.id = @CourtId
              AND c.deleted_at IS NULL;
            """;

        using var connection = sqlConnectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<CourtDetailResponse>(
            new CommandDefinition(sql, new { CourtId = courtId }, cancellationToken: cancellationToken));
    }

    public Task<bool> ExistsAsync(Guid courtId, CancellationToken cancellationToken) =>
        ScalarAsync<bool>(
            """
            SELECT EXISTS
            (
                SELECT 1
                FROM core.courts c
                WHERE c.id = @CourtId
                  AND c.deleted_at IS NULL
            );
            """,
            new { CourtId = courtId },
            cancellationToken);

    public Task<bool> BranchExistsAsync(Guid branchId, CancellationToken cancellationToken) =>
        ScalarAsync<bool>(
            """
            SELECT EXISTS
            (
                SELECT 1
                FROM core.branches b
                WHERE b.id = @BranchId
                  AND b.deleted_at IS NULL
            );
            """,
            new { BranchId = branchId },
            cancellationToken);

    public Task<bool> CourtTypeExistsAsync(Guid courtTypeId, CancellationToken cancellationToken) =>
        ScalarAsync<bool>(
            """
            SELECT EXISTS
            (
                SELECT 1
                FROM core.court_types ct
                WHERE ct.id = @CourtTypeId
                  AND ct.is_active = TRUE
            );
            """,
            new { CourtTypeId = courtTypeId },
            cancellationToken);

    public Task<bool> NameExistsAsync(
        Guid branchId,
        string name,
        Guid? excludeCourtId,
        CancellationToken cancellationToken) =>
        ScalarAsync<bool>(
            """
            SELECT EXISTS
            (
                SELECT 1
                FROM core.courts c
                WHERE c.branch_id = @BranchId
                  AND LOWER(c.name) = LOWER(@Name)
                  AND c.deleted_at IS NULL
                  AND (@ExcludeCourtId::uuid IS NULL OR c.id <> @ExcludeCourtId)
            );
            """,
            new { BranchId = branchId, Name = name, ExcludeCourtId = excludeCourtId },
            cancellationToken);

    // ux_courts_branch_number is not filtered by deleted_at, so soft-deleted courts still hold their number.
    public Task<bool> CourtNumberExistsAsync(
        Guid branchId,
        int courtNumber,
        Guid? excludeCourtId,
        CancellationToken cancellationToken) =>
        ScalarAsync<bool>(
            """
            SELECT EXISTS
            (
                SELECT 1
                FROM core.courts c
                WHERE c.branch_id = @BranchId
                  AND c.court_number = @CourtNumber
                  AND (@ExcludeCourtId::uuid IS NULL OR c.id <> @ExcludeCourtId)
            );
            """,
            new { BranchId = branchId, CourtNumber = courtNumber, ExcludeCourtId = excludeCourtId },
            cancellationToken);

    public Task<bool> HasFutureBookingsAsync(Guid courtId, CancellationToken cancellationToken) =>
        ScalarAsync<bool>(
            """
            SELECT EXISTS
            (
                SELECT 1
                FROM booking.booking_details bd
                INNER JOIN booking.bookings b ON b.id = bd.booking_id
                WHERE bd.court_id = @CourtId
                  AND bd.booking_date >= CURRENT_DATE
                  AND LOWER(bd.status) NOT IN ('cancelled', 'completed')
                  AND b.deleted_at IS NULL
                  AND LOWER(b.status) NOT IN ('cancelled', 'completed')
            );
            """,
            new { CourtId = courtId },
            cancellationToken);

    public Task<bool> HasFutureMatchesAsync(Guid courtId, CancellationToken cancellationToken) =>
        ScalarAsync<bool>(
            """
            SELECT EXISTS
            (
                SELECT 1
                FROM matching.player_matches pm
                WHERE pm.court_id = @CourtId
                  AND pm.match_date >= CURRENT_DATE
                  AND pm.status IN ('OPEN', 'FULL')
            );
            """,
            new { CourtId = courtId },
            cancellationToken);

    public async Task<Guid> CreateAsync(
        CreateCourtRequest request,
        Guid createdBy,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO core.courts
            (
                id,
                branch_id,
                court_type_id,
                court_number,
                name,
                status,
                is_active,
                created_at,
                created_by,
                updated_at,
                updated_by
            )
            VALUES
            (
                uuid_generate_v7(),
                @BranchId,
                @CourtTypeId,
                @CourtNumber,
                @Name,
                @Status,
                TRUE,
                NOW(),
                @CreatedBy,
                NOW(),
                @CreatedBy
            )
            RETURNING id;
            """;

        using var connection = sqlConnectionFactory.CreateConnection();

        try
        {
            return await connection.ExecuteScalarAsync<Guid>(new CommandDefinition(
                sql,
                new
                {
                    request.BranchId,
                    request.CourtTypeId,
                    request.CourtNumber,
                    request.Name,
                    request.Status,
                    CreatedBy = createdBy
                },
                cancellationToken: cancellationToken));
        }
        catch (PostgresException exception) when (IsCourtNumberConflict(exception))
        {
            throw new CourtNumberConflictException();
        }
    }

    public async Task<bool> UpdateAsync(
        Guid courtId,
        UpdateCourtRequest request,
        Guid updatedBy,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE core.courts
            SET
                court_type_id = @CourtTypeId,
                court_number = @CourtNumber,
                name = @Name,
                status = @Status,
                is_active = @IsActive,
                updated_at = NOW(),
                updated_by = @UpdatedBy
            WHERE id = @CourtId
              AND deleted_at IS NULL;
            """;

        using var connection = sqlConnectionFactory.CreateConnection();

        try
        {
            var affectedRows = await connection.ExecuteAsync(new CommandDefinition(
                sql,
                new
                {
                    CourtId = courtId,
                    request.CourtTypeId,
                    request.CourtNumber,
                    request.Name,
                    request.Status,
                    request.IsActive,
                    UpdatedBy = updatedBy
                },
                cancellationToken: cancellationToken));

            return affectedRows > 0;
        }
        catch (PostgresException exception) when (IsCourtNumberConflict(exception))
        {
            throw new CourtNumberConflictException();
        }
    }

    public async Task<bool> DeleteAsync(
        Guid courtId,
        Guid deletedBy,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE core.courts
            SET
                is_active = FALSE,
                deleted_at = NOW(),
                deleted_by = @DeletedBy,
                updated_at = NOW(),
                updated_by = @DeletedBy
            WHERE id = @CourtId
              AND deleted_at IS NULL;
            """;

        using var connection = sqlConnectionFactory.CreateConnection();
        var affectedRows = await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new { CourtId = courtId, DeletedBy = deletedBy },
            cancellationToken: cancellationToken));

        return affectedRows > 0;
    }

    private async Task<T> ScalarAsync<T>(string sql, object parameters, CancellationToken cancellationToken)
    {
        using var connection = sqlConnectionFactory.CreateConnection();
        return (await connection.ExecuteScalarAsync<T>(
            new CommandDefinition(sql, parameters, cancellationToken: cancellationToken)))!;
    }

    private static bool IsCourtNumberConflict(PostgresException exception) =>
        exception.SqlState == PostgresErrorCodes.UniqueViolation
        && exception.ConstraintName == CourtNumberUniqueConstraint;

    private static DynamicParameters CreateParameters(CourtSearchRequest request)
    {
        var parameters = new DynamicParameters();
        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            parameters.Add("Keyword", $"%{request.Keyword}%");
        }

        if (request.BranchId.HasValue)
        {
            parameters.Add("BranchId", request.BranchId.Value);
        }

        if (request.IsActive.HasValue)
        {
            parameters.Add("IsActive", request.IsActive.Value);
        }

        return parameters;
    }

    private static void AppendBaseQuery(StringBuilder sql)
    {
        sql.AppendLine("""
            WITH court_data AS
            (
                SELECT
                    c.id AS Id,
                    c.branch_id AS BranchId,
                    b.name AS BranchName,
                    c.court_number AS CourtNumber,
                    c.name AS Name,
                    c.court_type_id AS CourtTypeId,
                    ct.code AS CourtTypeCode,
                    ct.name AS CourtTypeName,
                    c.status AS Status,
                    c.is_active AS IsActive,
                    c.created_at AS CreatedAt
                FROM core.courts c
                INNER JOIN core.branches b ON b.id = c.branch_id
                LEFT JOIN core.court_types ct ON ct.id = c.court_type_id
                WHERE c.deleted_at IS NULL
                  AND b.deleted_at IS NULL
            )
            """);
    }

    private static void AppendFilters(StringBuilder sql, CourtSearchRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            sql.AppendLine("AND (Name ILIKE @Keyword OR BranchName ILIKE @Keyword OR CourtNumber::text ILIKE @Keyword)");
        }

        if (request.BranchId.HasValue)
        {
            sql.AppendLine("AND BranchId = @BranchId");
        }

        if (request.IsActive.HasValue)
        {
            sql.AppendLine("AND IsActive = @IsActive");
        }
    }

    private static string GetSortExpression(string sortBy) => sortBy switch
    {
        "oldest" => "ORDER BY CreatedAt ASC",
        "name_asc" => "ORDER BY Name ASC NULLS LAST, CourtNumber ASC",
        "name_desc" => "ORDER BY Name DESC NULLS LAST, CourtNumber DESC",
        _ => "ORDER BY CreatedAt DESC"
    };
}
