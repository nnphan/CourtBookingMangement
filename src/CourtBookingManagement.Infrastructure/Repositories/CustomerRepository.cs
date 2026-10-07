using System.Text;
using CourtBookingManagement.Application.Abstractions.Data;
using CourtBookingManagement.Application.Customers.Models.Requests;
using CourtBookingManagement.Application.Customers.Models.Responses;
using CourtBookingManagement.Application.Customers.Repositories;
using CourtBookingManagement.Application.Matching.Models;
using Dapper;

namespace CourtBookingManagement.Infrastructure.Repositories;

public sealed class CustomerRepository(ISqlConnectionFactory sqlConnectionFactory) : ICustomerRepository
{
    public async Task<PagedResult<CustomerListItemResponse>> SearchAsync(
        CustomerSearchRequest request,
        CancellationToken cancellationToken)
    {
        using var connection = sqlConnectionFactory.CreateConnection();

        var dataParameters = CreateParameters(request);
        var dataSql = new StringBuilder();
        AppendBaseQuery(dataSql);
        dataSql.AppendLine("SELECT * FROM customer_data WHERE 1 = 1");
        AppendFilters(dataSql, request);
        dataSql.AppendLine(GetSortExpression(request.SortBy));
        dataSql.AppendLine("LIMIT @PageSize OFFSET @Offset;");
        dataParameters.Add("PageSize", request.PageSize);
        dataParameters.Add("Offset", ((long)request.PageNumber - 1) * request.PageSize);

        var items = (await connection.QueryAsync<CustomerListItemResponse>(
            new CommandDefinition(dataSql.ToString(), dataParameters, cancellationToken: cancellationToken))).AsList();

        var countSql = new StringBuilder();
        AppendBaseQuery(countSql);
        countSql.AppendLine("SELECT COUNT(*) FROM customer_data WHERE 1 = 1");
        AppendFilters(countSql, request);

        var totalCount = await connection.ExecuteScalarAsync<long>(
            new CommandDefinition(countSql.ToString(), CreateParameters(request), cancellationToken: cancellationToken));

        return PagedResult<CustomerListItemResponse>.Create(items, request.PageNumber, request.PageSize, totalCount);
    }

    public async Task<CustomerDetailResponse?> GetByIdAsync(
        Guid customerId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                c.id AS Id,
                c.user_id AS UserId,
                c.full_name AS FullName,
                c.email AS Email,
                c.phone_number AS PhoneNumber,
                c.is_guest AS IsGuest,
                c.is_active AS IsActive,
                c.loyalty_points_balance AS LoyaltyPointsBalance,
                ml.id AS MembershipLevelId,
                ml.code AS MembershipLevelCode,
                ml.name AS MembershipLevelName,
                ml.discount_pct AS DiscountPercentage,
                u.is_email_verified AS IsEmailVerified,
                u.last_login_at AS LastLoginAt,
                c.created_at AS CreatedAt,
                c.updated_at AS UpdatedAt
            FROM customer.customers c
            LEFT JOIN customer.membership_levels ml ON ml.id = c.membership_level_id
            LEFT JOIN auth.users u ON u.id = c.user_id
            WHERE c.id = @CustomerId
              AND c.deleted_at IS NULL;
            """;

        using var connection = sqlConnectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<CustomerDetailResponse>(
            new CommandDefinition(sql, new { CustomerId = customerId }, cancellationToken: cancellationToken));
    }

    private static DynamicParameters CreateParameters(CustomerSearchRequest request)
    {
        var parameters = new DynamicParameters();
        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            parameters.Add("Keyword", $"%{request.Keyword}%");
        }

        if (request.MembershipLevelId.HasValue)
        {
            parameters.Add("MembershipLevelId", request.MembershipLevelId.Value);
        }

        if (request.IsActive.HasValue)
        {
            parameters.Add("IsActive", request.IsActive.Value);
        }

        if (request.IsGuest.HasValue)
        {
            parameters.Add("IsGuest", request.IsGuest.Value);
        }

        return parameters;
    }

    private static void AppendBaseQuery(StringBuilder sql)
    {
        sql.AppendLine("""
            WITH customer_data AS
            (
                SELECT
                    c.id AS Id,
                    c.full_name AS FullName,
                    c.email AS Email,
                    c.phone_number AS PhoneNumber,
                    c.is_guest AS IsGuest,
                    c.is_active AS IsActive,
                    c.loyalty_points_balance AS LoyaltyPointsBalance,
                    c.membership_level_id AS MembershipLevelId,
                    ml.code AS MembershipLevelCode,
                    ml.name AS MembershipLevelName,
                    c.created_at AS CreatedAt
                FROM customer.customers c
                LEFT JOIN customer.membership_levels ml ON ml.id = c.membership_level_id
                WHERE c.deleted_at IS NULL
            )
            """);
    }

    private static void AppendFilters(StringBuilder sql, CustomerSearchRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            sql.AppendLine("AND (FullName ILIKE @Keyword OR Email ILIKE @Keyword OR PhoneNumber ILIKE @Keyword)");
        }

        if (request.MembershipLevelId.HasValue)
        {
            sql.AppendLine("AND MembershipLevelId = @MembershipLevelId");
        }

        if (request.IsActive.HasValue)
        {
            sql.AppendLine("AND IsActive = @IsActive");
        }

        if (request.IsGuest.HasValue)
        {
            sql.AppendLine("AND IsGuest = @IsGuest");
        }
    }

    // Secondary keys keep pagination stable when the primary sort column has ties.
    private static string GetSortExpression(string sortBy) => sortBy switch
    {
        "oldest" => "ORDER BY CreatedAt ASC, Id ASC",
        "name_asc" => "ORDER BY FullName ASC, Id ASC",
        "name_desc" => "ORDER BY FullName DESC, Id DESC",
        "points_asc" => "ORDER BY LoyaltyPointsBalance ASC, CreatedAt DESC, Id DESC",
        "points_desc" => "ORDER BY LoyaltyPointsBalance DESC, CreatedAt DESC, Id DESC",
        _ => "ORDER BY CreatedAt DESC, Id DESC"
    };
}
