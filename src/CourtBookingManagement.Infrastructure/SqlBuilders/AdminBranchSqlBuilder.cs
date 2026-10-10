using CourtBookingManagement.Application.Admin.Branches.DTOs;
using Dapper;

namespace CourtBookingManagement.Infrastructure.SqlBuilders;

public static class AdminBranchSqlBuilder
{
    private const string BranchDataCte = """
        WITH branch_data AS
        (
            SELECT
                b.id,
                b.name,
                b.address,
                b.city,
                b.district,
                b.phone_number,
                b.is_active,
                b.created_at,
                COUNT(c.id) FILTER (WHERE c.deleted_at IS NULL)::int AS total_courts
            FROM core.branches b
            LEFT JOIN core.courts c ON c.branch_id = b.id
            WHERE b.deleted_at IS NULL
            GROUP BY b.id
        )
        """;

    public static (string Sql, DynamicParameters Parameters) BuildGetBranches(GetBranchesRequest request)
    {
        var filters = new List<string>();
        var parameters = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            filters.Add("(name ILIKE '%' || @Keyword || '%' OR address ILIKE '%' || @Keyword || '%' OR phone_number ILIKE '%' || @Keyword || '%')");
            parameters.Add("Keyword", request.Keyword);
        }

        if (!string.IsNullOrWhiteSpace(request.City))
        {
            filters.Add("city ILIKE '%' || @City || '%'");
            parameters.Add("City", request.City);
        }

        if (!string.IsNullOrWhiteSpace(request.District))
        {
            filters.Add("district ILIKE '%' || @District || '%'");
            parameters.Add("District", request.District);
        }

        if (request.IsActive.HasValue)
        {
            filters.Add("is_active = @IsActive");
            parameters.Add("IsActive", request.IsActive.Value);
        }

        var whereClause = filters.Count == 0
            ? string.Empty
            : $"WHERE {string.Join(" AND ", filters)}";
        var offset = ((long)request.PageNumber - 1) * request.PageSize;
        parameters.Add("PageSize", request.PageSize);
        parameters.Add("Offset", offset);

        var sql = $"""
            {BranchDataCte}
            SELECT
                id AS Id,
                name AS Name,
                city AS City,
                district AS District,
                phone_number AS PhoneNumber,
                total_courts AS TotalCourts,
                is_active AS IsActive,
                created_at AS CreatedAt
            FROM branch_data
            {whereClause}
            ORDER BY created_at DESC, name ASC, id ASC
            LIMIT @PageSize OFFSET @Offset;

            {BranchDataCte}
            SELECT COUNT(*)::bigint
            FROM branch_data
            {whereClause};
            """;

        return (sql, parameters);
    }

    public static string BuildGetSummary() => """
        SELECT
            (SELECT COUNT(*)::int FROM core.branches WHERE deleted_at IS NULL) AS TotalBranches,
            (SELECT COUNT(*)::int FROM core.branches WHERE deleted_at IS NULL AND is_active = true) AS ActiveBranches,
            (SELECT COUNT(*)::int FROM core.branches WHERE deleted_at IS NULL AND is_active = false) AS InactiveBranches,
            (SELECT COUNT(*)::int FROM core.courts WHERE deleted_at IS NULL) AS TotalCourts;
        """;
}