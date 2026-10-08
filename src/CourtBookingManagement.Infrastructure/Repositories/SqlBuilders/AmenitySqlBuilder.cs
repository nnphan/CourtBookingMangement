namespace CourtBookingManagement.Infrastructure.Repositories.SqlBuilders;

public static class AmenitySqlBuilder
{
    public static string BuildGetAll() => """
            SELECT
                a.id AS Id,
                a.code AS Code,
                a.name AS Name,
                a.icon AS Icon
            FROM core.amenities a
            ORDER BY a.name ASC;
            """;
}