using System.Text;
using CourtBookingManagement.Application.Abstractions.Data;
using CourtBookingManagement.Application.Matching.Models;
using CourtBookingManagement.Application.Permissions.Models.Requests;
using CourtBookingManagement.Application.Permissions.Models.Responses;
using CourtBookingManagement.Application.Permissions.Repositories;
using Dapper;

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
                    p.created_at
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
        "code_desc" => "ORDER BY Code DESC",
        "name_asc" => "ORDER BY Name ASC",
        "name_desc" => "ORDER BY Name DESC",
        "latest" => "ORDER BY CreatedAt DESC",
        "oldest" => "ORDER BY CreatedAt ASC",
        _ => "ORDER BY Code ASC"
    };
}