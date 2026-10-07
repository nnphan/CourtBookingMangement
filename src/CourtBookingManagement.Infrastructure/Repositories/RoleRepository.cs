using System.Text;
using CourtBookingManagement.Application.Abstractions.Data;
using CourtBookingManagement.Application.Matching.Models;
using CourtBookingManagement.Application.Roles.Models.Requests;
using CourtBookingManagement.Application.Roles.Models.Responses;
using CourtBookingManagement.Application.Roles.Repositories;
using Dapper;

namespace CourtBookingManagement.Infrastructure.Repositories;

public sealed class RoleRepository(ISqlConnectionFactory sqlConnectionFactory) : IRoleRepository
{
    public async Task<PagedResult<RoleResponse>> SearchAsync(
        RoleSearchRequest request,
        CancellationToken cancellationToken)
    {
        using var connection = sqlConnectionFactory.CreateConnection();
        var dataParameters = CreateParameters(request);
        var dataSql = new StringBuilder();

        AppendBaseQuery(dataSql);
        dataSql.AppendLine("SELECT * FROM role_data WHERE 1 = 1");
        AppendFilters(dataSql, dataParameters, request);
        dataSql.AppendLine(GetSortExpression(request.SortBy));
        dataSql.AppendLine("LIMIT @PageSize OFFSET @Offset;");
        dataParameters.Add("PageSize", request.PageSize);
        dataParameters.Add("Offset", ((long)request.Page - 1) * request.PageSize);

        var items = (await connection.QueryAsync<RoleResponse>(
            new CommandDefinition(dataSql.ToString(), dataParameters, cancellationToken: cancellationToken))).AsList();

        var countParameters = CreateParameters(request);
        var countSql = new StringBuilder();
        AppendBaseQuery(countSql);
        countSql.AppendLine("SELECT COUNT(*) FROM role_data WHERE 1 = 1");
        AppendFilters(countSql, countParameters, request);

        var totalCount = await connection.ExecuteScalarAsync<long>(
            new CommandDefinition(countSql.ToString(), countParameters, cancellationToken: cancellationToken));

        return PagedResult<RoleResponse>.Create(items, request.Page, request.PageSize, totalCount);
    }

    private static DynamicParameters CreateParameters(RoleSearchRequest request)
    {
        var parameters = new DynamicParameters();
        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            parameters.Add("Keyword", $"%{request.Keyword}%");
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
            WITH role_data AS
            (
                SELECT
                    r.id AS Id,
                    r.code AS Code,
                    r.name AS Name,
                    r.description AS Description,
                    r.is_active AS IsActive,
                    r.created_at AS CreatedAt,
                    COUNT(DISTINCT ur.user_id) AS UserCount,
                    COUNT(DISTINCT rp.permission_id) AS PermissionCount
                FROM auth.roles r
                LEFT JOIN auth.user_roles ur ON ur.role_id = r.id
                LEFT JOIN auth.role_permissions rp ON rp.role_id = r.id
                WHERE r.deleted_at IS NULL
                GROUP BY
                    r.id,
                    r.code,
                    r.name,
                    r.description,
                    r.is_active,
                    r.created_at
            )
            """);
    }

    private static void AppendFilters(StringBuilder sql, DynamicParameters parameters, RoleSearchRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            sql.AppendLine("AND (Code ILIKE @Keyword OR Name ILIKE @Keyword OR Description ILIKE @Keyword)");
        }

        if (request.IsActive.HasValue)
        {
            sql.AppendLine("AND IsActive = @IsActive");
        }
    }

    private static string GetSortExpression(string sortBy) => sortBy switch
    {
        "oldest" => "ORDER BY CreatedAt ASC",
        "name_asc" => "ORDER BY Name ASC",
        "name_desc" => "ORDER BY Name DESC",
        "code_asc" => "ORDER BY Code ASC",
        "code_desc" => "ORDER BY Code DESC",
        _ => "ORDER BY CreatedAt DESC"
    };

    public async Task<Guid?> GetRoleIdByCodeAsync(
        string roleCode,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT r.id
            FROM auth.roles r
            WHERE r.code = @RoleCode
              AND r.is_active = TRUE
              AND r.deleted_at IS NULL
            LIMIT 1;
            """;

        using var connection = sqlConnectionFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<Guid?>(
            new CommandDefinition(sql, new { RoleCode = roleCode }, cancellationToken: cancellationToken));
    }
}