using System.Text;
using CourtBookingManagement.Application.Abstractions.Data;
using CourtBookingManagement.Application.Matching.Models;
using CourtBookingManagement.Application.Matching.Models.Requests;
using CourtBookingManagement.Application.Matching.Models.Responses;
using CourtBookingManagement.Application.Matching.Repositories;
using Dapper;
using Microsoft.Extensions.Logging;

namespace CourtBookingManagement.Infrastructure.Repositories;

public sealed class PlayerMatchRepository(ISqlConnectionFactory sqlConnectionFactory) : IPlayerMatchRepository
{
    public async Task<PagedResult<PlayerMatchResponse>> SearchAsync(
        PlayerMatchSearchRequest request,
        CancellationToken cancellationToken)
    {
        var sql = new StringBuilder();
        var parameters = new DynamicParameters();

        AppendBaseQuery(sql, parameters);
        sql.AppendLine("SELECT * FROM match_data WHERE 1 = 1");
        AppendFilters(sql, parameters, request);
        AppendSort(sql, request.SortBy);
        sql.AppendLine("LIMIT @PageSize OFFSET @Offset");

        parameters.Add("PageSize", request.PageSize);
        parameters.Add("Offset", (request.Page - 1) * request.PageSize);

        using var connection = sqlConnectionFactory.CreateConnection();

        var items = (await connection.QueryAsync<PlayerMatchResponse>(
            new CommandDefinition(sql.ToString(), parameters, cancellationToken: cancellationToken))).AsList();

        var countSql = new StringBuilder();
        var countParameters = new DynamicParameters();

        AppendBaseQuery(countSql, countParameters);
        countSql.AppendLine("SELECT COUNT(*) FROM match_data WHERE 1 = 1");
        AppendFilters(countSql, countParameters, request);

        var totalCount = await connection.ExecuteScalarAsync<long>(
            new CommandDefinition(countSql.ToString(), countParameters, cancellationToken: cancellationToken));

        return PagedResult<PlayerMatchResponse>.Create(items, request.Page, request.PageSize, totalCount);
    }

    private static void AppendBaseQuery(StringBuilder sql, DynamicParameters parameters)
    {
        sql.AppendLine("""
            WITH match_data AS (
                SELECT
                    pm.id AS Id,
                    pm.title AS Title,
                    pm.branch_id AS BranchId,
                    b.name AS BranchName,
                    b.city AS City,
                    b.district AS District,
                    c.name AS CourtName,
                    pm.match_date AS MatchDate,
                    pm.start_time AS StartTime,
                    pm.end_time AS EndTime,
                    pm.skill_level AS SkillLevel,
                    pm.max_players AS MaxPlayers,
                    pm.current_players AS CurrentPlayers,
                    (pm.max_players - pm.current_players) AS RemainingSlots,
                    pm.fee_per_player AS FeePerPlayer,
                    pm.status AS Status,
                    pm.created_by AS CreatedBy,
                    u.full_name AS CreatedByName,
                    pm.created_at AS CreatedAt
                FROM matching.player_matches pm
                INNER JOIN core.branches b ON b.id = pm.branch_id
                LEFT JOIN core.courts c ON c.id = pm.court_id
                INNER JOIN auth.users u ON u.id = pm.created_by
                WHERE pm.status = @Status
                  AND pm.current_players < pm.max_players
                  AND pm.match_date >= CURRENT_DATE
            )
            """);

        parameters.Add("Status", "OPEN");
    }

    private static void AppendFilters(
        StringBuilder sql,
        DynamicParameters parameters,
        PlayerMatchSearchRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            sql.AppendLine(@"
                AND (
                    Title ILIKE @Keyword
                    OR BranchName ILIKE @Keyword
                    OR CourtName ILIKE @Keyword
                )");

            parameters.Add("Keyword", $"%{request.Keyword.Trim()}%");
        }

        if (request.MatchDate.HasValue)
        {
            sql.AppendLine("AND MatchDate = @MatchDate");
            parameters.Add("MatchDate", request.MatchDate.Value);
        }

        if (request.StartTime.HasValue)
        {
            sql.AppendLine("AND StartTime >= @StartTime");
            parameters.Add("StartTime", request.StartTime.Value);
        }

        if (request.EndTime.HasValue)
        {
            sql.AppendLine("AND EndTime <= @EndTime");
            parameters.Add("EndTime", request.EndTime.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.SkillLevel))
        {
            sql.AppendLine("AND SkillLevel = @SkillLevel");
            parameters.Add("SkillLevel", request.SkillLevel.Trim());
        }

        if (!string.IsNullOrWhiteSpace(request.City))
        {
            sql.AppendLine("AND City ILIKE @City");
            parameters.Add("City", $"%{request.City.Trim()}%");
        }

        if (!string.IsNullOrWhiteSpace(request.District))
        {
            sql.AppendLine("AND District ILIKE @District");
            parameters.Add("District", $"%{request.District.Trim()}%");
        }
    }

    private static void AppendSort(StringBuilder sql, string? sortBy)
    {
        switch (sortBy?.Trim().ToLowerInvariant())
        {
            case "oldest":
                sql.AppendLine("ORDER BY CreatedAt ASC");
                break;
            case "match_date_asc":
                sql.AppendLine("ORDER BY MatchDate ASC, StartTime ASC");
                break;
            case "match_date_desc":
                sql.AppendLine("ORDER BY MatchDate DESC, StartTime DESC");
                break;
            default:
                sql.AppendLine("ORDER BY CreatedAt DESC");
                break;
        }
    }
}
