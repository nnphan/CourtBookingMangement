using System.Text;
using CourtBookingManagement.Application.Abstractions.Data;
using CourtBookingManagement.Application.Matching.Models;
using CourtBookingManagement.Application.Matching.Models.Requests;
using CourtBookingManagement.Application.Matching.Models.Responses;
using CourtBookingManagement.Application.Matching.Repositories;
using Dapper;

namespace CourtBookingManagement.Infrastructure.Repositories;

public sealed class MyMatchRepository(ISqlConnectionFactory sqlConnectionFactory) : IMyMatchRepository
{
    public async Task<PagedResult<MyMatchResponse>> GetMyMatchesAsync(
        Guid userId,
        MyMatchSearchRequest request,
        CancellationToken cancellationToken)
    {
        using var connection = sqlConnectionFactory.CreateConnection();
        var queryParameters = CreateParameters(userId, request);
        var dataSql = new StringBuilder();

        AppendBaseQuery(dataSql);
        dataSql.AppendLine("SELECT * FROM my_matches WHERE 1 = 1");
        AppendFilters(dataSql, queryParameters, request);
        dataSql.AppendLine(GetSortExpression(request.SortBy));
        dataSql.AppendLine("LIMIT @PageSize OFFSET @Offset;");
        queryParameters.Add("PageSize", request.PageSize);
        queryParameters.Add("Offset", ((long)request.Page - 1) * request.PageSize);

        var items = (await connection.QueryAsync<MyMatchResponse>(
            new CommandDefinition(dataSql.ToString(), queryParameters, cancellationToken: cancellationToken))).AsList();

        var countParameters = CreateParameters(userId, request);
        var countSql = new StringBuilder();
        AppendBaseQuery(countSql);
        countSql.AppendLine("SELECT COUNT(*) FROM my_matches WHERE 1 = 1");
        AppendFilters(countSql, countParameters, request);

        var totalCount = await connection.ExecuteScalarAsync<long>(
            new CommandDefinition(countSql.ToString(), countParameters, cancellationToken: cancellationToken));

        return PagedResult<MyMatchResponse>.Create(items, request.Page, request.PageSize, totalCount);
    }

    private static DynamicParameters CreateParameters(Guid userId, MyMatchSearchRequest request)
    {
        var parameters = new DynamicParameters();
        parameters.Add("UserId", userId);
        if (request.Status is not null)
        {
            parameters.Add("Status", request.Status);
        }

        if (request.MatchDate.HasValue)
        {
            parameters.Add("MatchDate", request.MatchDate.Value);
        }

        return parameters;
    }

    private static void AppendBaseQuery(StringBuilder sql)
    {
        sql.AppendLine("""
            WITH my_matches AS
            (
                SELECT DISTINCT
                    pm.id AS Id,
                    pm.title AS Title,
                    pm.branch_id AS BranchId,
                    b.name AS BranchName,
                    c.name AS CourtName,
                    pm.match_date AS MatchDate,
                    pm.start_time AS StartTime,
                    pm.end_time AS EndTime,
                    pm.skill_level AS SkillLevel,
                    pm.status AS Status,
                    pm.max_players AS MaxPlayers,
                    pm.current_players AS CurrentPlayers,
                    GREATEST(pm.max_players - pm.current_players, 0) AS RemainingSlots,
                    pm.fee_per_player AS FeePerPlayer,
                    pm.created_by AS CreatedBy,
                    pm.created_at AS CreatedAt,
                    pm.created_by = @UserId AS IsHost,
                    EXISTS
                    (
                        SELECT 1
                        FROM matching.match_participants mp
                        WHERE mp.match_id = pm.id
                          AND mp.user_id = @UserId
                          AND mp.status = 'ACTIVE'
                    ) AS Joined,
                    EXISTS
                    (
                        SELECT 1
                        FROM matching.match_join_requests mjr
                        WHERE mjr.match_id = pm.id
                          AND mjr.user_id = @UserId
                          AND mjr.status = 'PENDING'
                    ) AS PendingApproval
                FROM matching.player_matches pm
                INNER JOIN core.branches b ON b.id = pm.branch_id
                LEFT JOIN core.courts c ON c.id = pm.court_id
                WHERE pm.created_by = @UserId
                   OR EXISTS
                   (
                       SELECT 1
                       FROM matching.match_participants mp
                       WHERE mp.match_id = pm.id
                         AND mp.user_id = @UserId
                         AND mp.status = 'ACTIVE'
                   )
                   OR EXISTS
                   (
                       SELECT 1
                       FROM matching.match_join_requests mjr
                       WHERE mjr.match_id = pm.id
                         AND mjr.user_id = @UserId
                         AND mjr.status = 'PENDING'
                   )
            )
            """);
    }

    private static void AppendFilters(StringBuilder sql, DynamicParameters parameters, MyMatchSearchRequest request)
    {
        switch (request.Type)
        {
            case "Hosted":
                sql.AppendLine("AND IsHost = TRUE");
                break;
            case "Joined":
                sql.AppendLine("AND Joined = TRUE");
                break;
            case "Pending":
                sql.AppendLine("AND PendingApproval = TRUE");
                break;
        }

        if (request.Status is not null)
        {
            sql.AppendLine("AND Status = @Status");
        }

        if (request.MatchDate.HasValue)
        {
            sql.AppendLine("AND MatchDate = @MatchDate");
        }
    }

    private static string GetSortExpression(string sortBy) => sortBy switch
    {
        "oldest" => "ORDER BY CreatedAt ASC",
        "match_date_asc" => "ORDER BY MatchDate ASC, StartTime ASC",
        "match_date_desc" => "ORDER BY MatchDate DESC, StartTime DESC",
        _ => "ORDER BY CreatedAt DESC"
    };
}