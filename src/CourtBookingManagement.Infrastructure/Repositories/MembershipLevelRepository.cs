using CourtBookingManagement.Application.Abstractions.Data;
using CourtBookingManagement.Application.Customers.Repositories;
using Dapper;

namespace CourtBookingManagement.Infrastructure.Repositories;

public sealed class MembershipLevelRepository(ISqlConnectionFactory sqlConnectionFactory) : IMembershipLevelRepository
{
    public async Task<Guid?> GetDefaultMembershipLevelIdAsync(
        CancellationToken cancellationToken)
    {
        // The entry tier is the lowest sort_order; min_points and id break ties deterministically.
        const string sql = """
            SELECT ml.id
            FROM customer.membership_levels ml
            ORDER BY ml.sort_order ASC, ml.min_points ASC, ml.id ASC
            LIMIT 1;
            """;

        using var connection = sqlConnectionFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<Guid?>(
            new CommandDefinition(sql, cancellationToken: cancellationToken));
    }
}
