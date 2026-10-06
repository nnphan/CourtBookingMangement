using System.Text;
using CourtBookingManagement.Application.Abstractions.Data;
using CourtBookingManagement.Application.Matching.Models;
using CourtBookingManagement.Application.Notifications.Models.Requests;
using CourtBookingManagement.Application.Notifications.Models.Responses;
using CourtBookingManagement.Application.Notifications.Repositories;
using Dapper;

namespace CourtBookingManagement.Infrastructure.Repositories;

public sealed class NotificationRepository(ISqlConnectionFactory sqlConnectionFactory) : INotificationRepository
{
    public async Task<PagedResult<NotificationResponse>> GetNotificationsAsync(
        Guid userId,
        NotificationSearchRequest request,
        CancellationToken cancellationToken)
    {
        using var connection = sqlConnectionFactory.CreateConnection();
        var parameters = CreateParameters(userId, request);
        var dataSql = new StringBuilder();

        AppendBaseQuery(dataSql);
        dataSql.AppendLine("SELECT * FROM notification_data WHERE 1 = 1");
        AppendFilters(dataSql, parameters, request);
        dataSql.AppendLine("ORDER BY CreatedAt DESC");
        dataSql.AppendLine("LIMIT @PageSize OFFSET @Offset;");
        parameters.Add("PageSize", request.PageSize);
        parameters.Add("Offset", ((long)request.Page - 1) * request.PageSize);

        var items = (await connection.QueryAsync<NotificationResponse>(
            new CommandDefinition(dataSql.ToString(), parameters, cancellationToken: cancellationToken))).AsList();

        var countParameters = CreateParameters(userId, request);
        var countSql = new StringBuilder();
        AppendBaseQuery(countSql);
        countSql.AppendLine("SELECT COUNT(*) FROM notification_data WHERE 1 = 1");
        AppendFilters(countSql, countParameters, request);

        var totalCount = await connection.ExecuteScalarAsync<long>(
            new CommandDefinition(countSql.ToString(), countParameters, cancellationToken: cancellationToken));

        return PagedResult<NotificationResponse>.Create(items, request.Page, request.PageSize, totalCount);
    }

    public async Task<int> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COUNT(*)
            FROM matching.match_notifications
            WHERE user_id = @UserId
              AND is_read = FALSE;
            """;

        using var connection = sqlConnectionFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, new { UserId = userId }, cancellationToken: cancellationToken));
    }

    private static DynamicParameters CreateParameters(Guid userId, NotificationSearchRequest request)
    {
        var parameters = new DynamicParameters();
        parameters.Add("UserId", userId);

        if (request.IsRead.HasValue)
        {
            parameters.Add("IsRead", request.IsRead.Value);
        }

        if (request.Type is not null)
        {
            parameters.Add("Type", request.Type);
        }

        return parameters;
    }

    private static void AppendBaseQuery(StringBuilder sql)
    {
        sql.AppendLine("""
            WITH notification_data AS
            (
                SELECT
                    mn.id AS Id,
                    mn.match_id AS MatchId,
                    mn.type AS NotificationType,
                    mn.title AS Title,
                    mn.message AS Message,
                    mn.is_read AS IsRead,
                    mn.created_at AS CreatedAt
                FROM matching.match_notifications mn
                WHERE mn.user_id = @UserId
            )
            """);
    }

    private static void AppendFilters(StringBuilder sql, DynamicParameters parameters, NotificationSearchRequest request)
    {
        if (request.IsRead.HasValue)
        {
            sql.AppendLine("AND IsRead = @IsRead");
        }

        if (request.Type is not null)
        {
            sql.AppendLine("AND NotificationType = @Type");
        }
    }
}