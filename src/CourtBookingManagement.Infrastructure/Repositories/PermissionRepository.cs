using System.Text;
using CourtBookingManagement.Application.Abstractions.Data;
using CourtBookingManagement.Application.Matching.Models;
using CourtBookingManagement.Application.Permissions.Models.Requests;
using CourtBookingManagement.Application.Permissions.Models.Responses;
using CourtBookingManagement.Application.Permissions.Repositories;
using Dapper;
using Npgsql;

namespace CourtBookingManagement.Infrastructure.Repositories;

public sealed class PermissionRepository(ISqlConnectionFactory sqlConnectionFactory) : IPermissionRepository
{
    public async Task<PagedResult<PermissionResponse>> SearchAsync(
        PermissionSearchRequest request,
        CancellationToken cancellationToken)
    {
        using var connection = sqlConnectionFactory.CreateConnection();
        var dataParameters = CreateParameters(request);
        var dataSql = new StringBuilder();

        AppendBaseQuery(dataSql);
        dataSql.AppendLine("SELECT * FROM permission_data WHERE 1 = 1");
        AppendFilters(dataSql, dataParameters, request);
        dataSql.AppendLine(GetSortExpression(request.SortBy));
        dataSql.AppendLine("LIMIT @PageSize OFFSET @Offset;");
        dataParameters.Add("PageSize", request.PageSize);
        dataParameters.Add("Offset", ((long)request.PageNumber - 1) * request.PageSize);

        var items = (await connection.QueryAsync<PermissionResponse>(
            new CommandDefinition(dataSql.ToString(), dataParameters, cancellationToken: cancellationToken))).AsList();

        var countParameters = CreateParameters(request);
        var countSql = new StringBuilder();
        AppendBaseQuery(countSql);
        countSql.AppendLine("SELECT COUNT(*) FROM permission_data WHERE 1 = 1");
        AppendFilters(countSql, countParameters, request);

        var totalCount = await connection.ExecuteScalarAsync<long>(
            new CommandDefinition(countSql.ToString(), countParameters, cancellationToken: cancellationToken));

        return PagedResult<PermissionResponse>.Create(items, request.PageNumber, request.PageSize, totalCount);
    }

    public async Task<PermissionResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                p.id AS Id,
                p.code AS Code,
                p.name AS Name,
                p.description AS Description,
                (
                    SELECT COUNT(DISTINCT rp.role_id)::int
                    FROM auth.role_permissions rp
                    INNER JOIN auth.roles r ON r.id = rp.role_id
                    WHERE rp.permission_id = p.id
                      AND r.is_active = TRUE
                      AND r.deleted_at IS NULL
                ) AS RoleCount,
                p.created_at AS CreatedAt,
                p.updated_at AS UpdatedAt
            FROM auth.permissions p
            WHERE p.id = @Id;
            """;

        using var connection = sqlConnectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<PermissionResponse>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<bool> ExistsByCodeAsync(string code, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT EXISTS (
                SELECT 1
                FROM auth.permissions
                WHERE code = @Code
            );
            """;

        using var connection = sqlConnectionFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(sql, new { Code = code }, cancellationToken: cancellationToken));
    }

    public async Task<PermissionWriteResult> CreateAsync(
        CreatePermissionRequest request,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO auth.permissions (code, name, description, created_at, updated_at)
            VALUES (@Code, @Name, @Description, NOW(), NOW())
            RETURNING
                id AS Id,
                code AS Code,
                name AS Name,
                description AS Description,
                0 AS RoleCount,
                created_at AS CreatedAt,
                updated_at AS UpdatedAt;
            """;

        using var connection = sqlConnectionFactory.CreateConnection();
        try
        {
            var permission = await connection.QuerySingleAsync<PermissionResponse>(
                new CommandDefinition(sql, request, cancellationToken: cancellationToken));
            return new PermissionWriteResult(PermissionWriteStatus.Success, permission);
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return new PermissionWriteResult(PermissionWriteStatus.CodeAlreadyExists);
        }
    }

    public async Task<PermissionWriteResult> UpdateAsync(
        Guid id,
        UpdatePermissionRequest request,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE auth.permissions
            SET name = @Name,
                description = @Description,
                updated_at = NOW()
            WHERE id = @Id
            RETURNING
                id AS Id,
                code AS Code,
                name AS Name,
                description AS Description,
                (
                    SELECT COUNT(DISTINCT rp.role_id)::int
                    FROM auth.role_permissions rp
                    INNER JOIN auth.roles r ON r.id = rp.role_id
                    WHERE rp.permission_id = auth.permissions.id
                      AND r.is_active = TRUE
                      AND r.deleted_at IS NULL
                ) AS RoleCount,
                created_at AS CreatedAt,
                updated_at AS UpdatedAt;
            """;

        using var connection = sqlConnectionFactory.CreateConnection();
        var permission = await connection.QuerySingleOrDefaultAsync<PermissionResponse>(
            new CommandDefinition(sql, new { Id = id, request.Name, request.Description }, cancellationToken: cancellationToken));

        return permission is null
            ? new PermissionWriteResult(PermissionWriteStatus.NotFound)
            : new PermissionWriteResult(PermissionWriteStatus.Success, permission);
    }

    public async Task<PermissionDeleteStatus> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        using var connection = sqlConnectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        var exists = await connection.QuerySingleOrDefaultAsync<Guid?>(
            new CommandDefinition(
                "SELECT id FROM auth.permissions WHERE id = @Id FOR UPDATE;",
                new { Id = id },
                transaction,
                cancellationToken: cancellationToken));
        if (exists is null)
        {
            return PermissionDeleteStatus.NotFound;
        }

        var inUse = await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                "SELECT EXISTS (SELECT 1 FROM auth.role_permissions WHERE permission_id = @Id);",
                new { Id = id },
                transaction,
                cancellationToken: cancellationToken));
        if (inUse)
        {
            return PermissionDeleteStatus.InUse;
        }

        try
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "DELETE FROM auth.permissions WHERE id = @Id;",
                new { Id = id },
                transaction,
                cancellationToken: cancellationToken));
            transaction.Commit();
            return PermissionDeleteStatus.Deleted;
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.ForeignKeyViolation)
        {
            transaction.Rollback();
            return PermissionDeleteStatus.InUse;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private static DynamicParameters CreateParameters(PermissionSearchRequest request)
    {
        var parameters = new DynamicParameters();
        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            parameters.Add("Keyword", $"%{request.Keyword}%");
        }

        if (request.RoleId.HasValue)
        {
            parameters.Add("RoleId", request.RoleId.Value);
        }

        return parameters;
    }

    private static void AppendBaseQuery(StringBuilder sql)
    {
        sql.AppendLine("""
            WITH permission_data AS
            (
                SELECT
                    p.id AS Id,
                    p.code AS Code,
                    p.name AS Name,
                    p.description AS Description,
                    p.created_at AS CreatedAt,
                    p.updated_at AS UpdatedAt,
                    COUNT(DISTINCT CASE
                        WHEN r.is_active = TRUE AND r.deleted_at IS NULL THEN rp.role_id
                    END) AS RoleCount
                FROM auth.permissions p
                LEFT JOIN auth.role_permissions rp ON rp.permission_id = p.id
                LEFT JOIN auth.roles r ON r.id = rp.role_id
                GROUP BY
                    p.id,
                    p.code,
                    p.name,
                    p.description,
                    p.created_at,
                    p.updated_at
            )
            """);
    }

    private static void AppendFilters(StringBuilder sql, DynamicParameters parameters, PermissionSearchRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            sql.AppendLine("AND (Code ILIKE @Keyword OR Name ILIKE @Keyword OR Description ILIKE @Keyword)");
        }

        if (request.RoleId.HasValue)
        {
            sql.AppendLine("""
                AND EXISTS
                (
                    SELECT 1
                    FROM auth.role_permissions rp
                    WHERE rp.permission_id = permission_data.Id
                      AND rp.role_id = @RoleId
                )
                """);
        }
    }

    private static string GetSortExpression(string sortBy) => sortBy switch
    {
        "code_desc" => "ORDER BY Code DESC, Id DESC",
        "name_asc" => "ORDER BY Name ASC, Id ASC",
        "name_desc" => "ORDER BY Name DESC, Id DESC",
        "latest" => "ORDER BY CreatedAt DESC, Id DESC",
        "oldest" => "ORDER BY CreatedAt ASC, Id ASC",
        _ => "ORDER BY Code ASC, Id ASC"
    };
}