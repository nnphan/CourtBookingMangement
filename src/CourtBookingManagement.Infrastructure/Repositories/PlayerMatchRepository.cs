using System.Data;
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
    public async Task<PlayerMatchDetailResponse?> GetByIdAsync(Guid matchId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                pm.id AS Id,
                pm.title AS Title,
                pm.description AS Description,
                pm.branch_id AS BranchId,
                b.name AS BranchName,
                b.city AS City,
                b.district AS District,
                c.name AS CourtName,
                pm.match_date AS MatchDate,
                pm.start_time AS StartTime,
                pm.end_time AS EndTime,
                pm.skill_level AS SkillLevel,
                pm.gender_preference AS GenderPreference,
                pm.max_players AS MaxPlayers,
                pm.current_players AS CurrentPlayers,
                pm.fee_per_player AS FeePerPlayer,
                pm.status AS Status,
                pm.created_by AS CreatedBy,
                u.full_name AS CreatedByName,
                pm.created_at AS CreatedAt
            FROM matching.player_matches pm
            INNER JOIN core.branches b ON b.id = pm.branch_id
            LEFT JOIN core.courts c ON c.id = pm.court_id
            INNER JOIN auth.users u ON u.id = pm.created_by
            WHERE pm.id = @MatchId;

            SELECT
                p.user_id AS UserId,
                u.full_name AS FullName,
                p.role AS Role,
                p.status AS Status,
                p.joined_at AS JoinedAt
            FROM matching.match_participants p
            INNER JOIN auth.users u ON u.id = p.user_id
            WHERE p.match_id = @MatchId
            ORDER BY p.joined_at;
            """;

        using var connection = sqlConnectionFactory.CreateConnection();
        using var results = await connection.QueryMultipleAsync(
            new CommandDefinition(sql, new { MatchId = matchId }, cancellationToken: cancellationToken));

        var match = await results.ReadSingleOrDefaultAsync<PlayerMatchDetailRow>();
        if (match is null)
        {
            return null;
        }

        var participants = (await results.ReadAsync<PlayerMatchParticipantRow>()).AsList();

        return new PlayerMatchDetailResponse
        {
            Id = match.Id,
            Title = match.Title,
            Description = match.Description,
            BranchId = match.BranchId,
            BranchName = match.BranchName,
            City = match.City,
            District = match.District,
            CourtName = match.CourtName,
            MatchDate = match.MatchDate,
            StartTime = match.StartTime,
            EndTime = match.EndTime,
            SkillLevel = match.SkillLevel,
            GenderPreference = match.GenderPreference,
            MaxPlayers = match.MaxPlayers,
            CurrentPlayers = match.CurrentPlayers,
            RemainingSlots = Math.Max(match.MaxPlayers - match.CurrentPlayers, 0),
            FeePerPlayer = match.FeePerPlayer ?? 0m,
            Status = match.Status,
            CreatedBy = match.CreatedBy,
            CreatedByName = match.CreatedByName,
            CreatedAt = match.CreatedAt,
            Participants = participants
                .Select(participant => new PlayerMatchParticipantResponse
                {
                    UserId = participant.UserId,
                    FullName = participant.FullName,
                    Role = participant.Role,
                    Status = participant.Status,
                    JoinedAt = participant.JoinedAt
                })
                .ToArray(),
            ParticipantCount = participants.Count,
            CanJoin = string.Equals(match.Status, "OPEN", StringComparison.OrdinalIgnoreCase)
                && match.CurrentPlayers < match.MaxPlayers,
            IsFull = match.CurrentPlayers >= match.MaxPlayers
        };
    }

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

    public Task<PlayerMatchDetailResponse?> GetMatchByIdAsync(Guid matchId, CancellationToken cancellationToken) =>
        GetByIdAsync(matchId, cancellationToken);

    public async Task<PlayerMatchJoinRequest?> GetJoinRequestAsync(Guid requestId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                id AS Id,
                match_id AS MatchId,
                user_id AS UserId,
                status AS Status,
                reviewed_at AS ReviewedAt,
                reviewed_by AS ReviewedBy
            FROM matching.match_join_requests
            WHERE id = @RequestId;
            """;

        using var connection = sqlConnectionFactory.CreateConnection();

        return await connection.QuerySingleOrDefaultAsync<PlayerMatchJoinRequest>(
            new CommandDefinition(sql, new { RequestId = requestId }, cancellationToken: cancellationToken));
    }

    public Task<bool> IsParticipantAsync(Guid matchId, Guid userId, CancellationToken cancellationToken) =>
        ScalarAsync<bool>(
            "SELECT EXISTS (SELECT 1 FROM matching.match_participants WHERE match_id = @MatchId AND user_id = @UserId AND status = 'ACTIVE');",
            new { MatchId = matchId, UserId = userId },
            cancellationToken);

    public Task<bool> HasPendingJoinRequestAsync(Guid matchId, Guid userId, CancellationToken cancellationToken) =>
        ScalarAsync<bool>(
            "SELECT EXISTS (SELECT 1 FROM matching.match_join_requests WHERE match_id = @MatchId AND user_id = @UserId AND status = 'PENDING');",
            new { MatchId = matchId, UserId = userId },
            cancellationToken);

    public Task<bool> BranchExistsAsync(Guid branchId, CancellationToken cancellationToken) =>
        ScalarAsync<bool>(
            "SELECT EXISTS (SELECT 1 FROM core.branches WHERE id = @BranchId AND is_active = TRUE AND deleted_at IS NULL);",
            new { BranchId = branchId },
            cancellationToken);

    public Task<bool> CourtExistsAsync(Guid courtId, Guid branchId, CancellationToken cancellationToken) =>
        ScalarAsync<bool>(
            "SELECT EXISTS (SELECT 1 FROM core.courts WHERE id = @CourtId AND branch_id = @BranchId AND is_active = TRUE AND deleted_at IS NULL);",
            new { CourtId = courtId, BranchId = branchId },
            cancellationToken);

    public Task<bool> HasOverlappingMatchAsync(Guid? courtId, DateOnly matchDate, TimeOnly startTime, TimeOnly endTime, CancellationToken cancellationToken)
    {
        if (courtId is null)
        {
            return Task.FromResult(false);
        }

        const string sql = """
            SELECT EXISTS (
                SELECT 1
                FROM matching.player_matches pm
                WHERE pm.court_id = @CourtId
                  AND pm.match_date = @MatchDate
                  AND pm.status IN ('OPEN', 'FULL')
                  AND @StartTime < pm.end_time
                  AND @EndTime > pm.start_time
            );
            """;

        return ScalarAsync<bool>(sql, new { CourtId = courtId.Value, MatchDate = matchDate, StartTime = startTime, EndTime = endTime }, cancellationToken);
    }

    public Task<bool> DuplicateMatchExistsAsync(Guid createdBy, Guid? courtId, DateOnly matchDate, TimeOnly startTime, TimeOnly endTime, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT EXISTS (
                SELECT 1
                FROM matching.player_matches pm
                WHERE pm.created_by = @CreatedBy
                  AND pm.match_date = @MatchDate
                  AND pm.status IN ('OPEN', 'FULL')
                  AND @StartTime < pm.end_time
                  AND @EndTime > pm.start_time
                  AND (@CourtId IS NULL OR pm.court_id = @CourtId OR pm.court_id IS NULL)
            );
            """;

        return ScalarAsync<bool>(sql, new { CreatedBy = createdBy, CourtId = courtId, MatchDate = matchDate, StartTime = startTime, EndTime = endTime }, cancellationToken);
    }

    public async Task<Guid> CreateAsync(CreatePlayerMatchRequest request, Guid createdBy, IDbTransaction transaction, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO matching.player_matches (
                id,
                created_by,
                branch_id,
                court_id,
                title,
                description,
                match_date,
                start_time,
                end_time,
                skill_level,
                gender_preference,
                max_players,
                current_players,
                fee_per_player,
                status,
                created_at,
                updated_at
            )
            VALUES (
                uuid_generate_v7(),
                @CreatedBy,
                @BranchId,
                @CourtId,
                @Title,
                @Description,
                @MatchDate,
                @StartTime,
                @EndTime,
                @SkillLevel,
                @GenderPreference,
                @MaxPlayers,
                1,
                @FeePerPlayer,
                'OPEN',
                NOW(),
                NOW()
            )
            RETURNING id;
            """;

        var command = new CommandDefinition(sql, new
        {
            CreatedBy = createdBy,
            BranchId = request.BranchId,
            CourtId = request.CourtId,
            Title = request.Title.Trim(),
            Description = request.Description,
            MatchDate = request.MatchDate,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            SkillLevel = request.SkillLevel.ToDatabaseValue(),
            GenderPreference = request.GenderPreference,
            MaxPlayers = request.MaxPlayers,
            FeePerPlayer = request.FeePerPlayer
        }, transaction: transaction, cancellationToken: cancellationToken);

        return await transaction.Connection!.QuerySingleAsync<Guid>(command);
    }

    public async Task<Guid> CreateJoinRequestAsync(Guid matchId, Guid userId, IDbTransaction transaction, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO matching.match_join_requests (
                id,
                match_id,
                user_id,
                status,
                requested_at,
                reviewed_at,
                reviewed_by
            )
            VALUES (
                uuid_generate_v7(),
                @MatchId,
                @UserId,
                'PENDING',
                NOW(),
                NULL,
                NULL
            )
            RETURNING id;
            """;

        var command = new CommandDefinition(sql, new { MatchId = matchId, UserId = userId }, transaction: transaction, cancellationToken: cancellationToken);
        return await transaction.Connection!.QuerySingleAsync<Guid>(command);
    }

    public async Task CreateParticipantAsync(Guid matchId, Guid userId, IDbTransaction transaction, CancellationToken cancellationToken)
    {
        await CreateParticipantAsync(matchId, userId, "HOST", transaction, cancellationToken);
    }

    public async Task CreateParticipantAsync(Guid matchId, Guid userId, string role, IDbTransaction transaction, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO matching.match_participants (
                id,
                match_id,
                user_id,
                joined_at,
                role,
                status
            )
            VALUES (
                uuid_generate_v7(),
                @MatchId,
                @UserId,
                NOW(),
                @Role,
                'ACTIVE'
            );
            """;

        var command = new CommandDefinition(sql, new { MatchId = matchId, UserId = userId, Role = role }, transaction: transaction, cancellationToken: cancellationToken);
        await transaction.Connection!.ExecuteAsync(command);
    }

    public async Task ApproveJoinRequestAsync(Guid requestId, Guid currentUserId, IDbTransaction transaction, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE matching.match_join_requests
            SET status = 'APPROVED',
                reviewed_at = NOW(),
                reviewed_by = @CurrentUserId
            WHERE id = @RequestId;
            """;

        var command = new CommandDefinition(sql, new { RequestId = requestId, CurrentUserId = currentUserId }, transaction: transaction, cancellationToken: cancellationToken);
        await transaction.Connection!.ExecuteAsync(command);
    }

    public async Task IncrementCurrentPlayersAsync(Guid matchId, IDbTransaction transaction, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE matching.player_matches
            SET current_players = current_players + 1,
                updated_at = NOW()
            WHERE id = @MatchId;
            """;

        var command = new CommandDefinition(sql, new { MatchId = matchId }, transaction: transaction, cancellationToken: cancellationToken);
        await transaction.Connection!.ExecuteAsync(command);
    }

    public async Task UpdateMatchStatusAsync(Guid matchId, string status, IDbTransaction transaction, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE matching.player_matches
            SET status = @Status,
                updated_at = NOW()
            WHERE id = @MatchId;
            """;

        var command = new CommandDefinition(sql, new { MatchId = matchId, Status = status }, transaction: transaction, cancellationToken: cancellationToken);
        await transaction.Connection!.ExecuteAsync(command);
    }

    public async Task CreateNotificationAsync(Guid matchId, Guid hostUserId, string message, IDbTransaction transaction, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO matching.match_notifications (
                id,
                user_id,
                match_id,
                type,
                title,
                message,
                is_read,
                created_at,
                read_at
            )
            VALUES (
                uuid_generate_v7(),
                @HostUserId,
                @MatchId,
                'JOIN_REQUEST',
                'Join request',
                @Message,
                FALSE,
                NOW(),
                NULL
            );
            """;

        var command = new CommandDefinition(sql, new { MatchId = matchId, HostUserId = hostUserId, Message = message }, transaction: transaction, cancellationToken: cancellationToken);
        await transaction.Connection!.ExecuteAsync(command);
    }

    public async Task CreateNotificationAsync(Guid userId, Guid? matchId, string type, string title, string message, IDbTransaction transaction, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO matching.match_notifications (
                id,
                user_id,
                match_id,
                type,
                title,
                message,
                is_read,
                created_at,
                read_at
            )
            VALUES (
                uuid_generate_v7(),
                @UserId,
                @MatchId,
                @Type,
                @Title,
                @Message,
                FALSE,
                NOW(),
                NULL
            );
            """;

        var command = new CommandDefinition(sql, new { UserId = userId, MatchId = matchId, Type = type, Title = title, Message = message }, transaction: transaction, cancellationToken: cancellationToken);
        await transaction.Connection!.ExecuteAsync(command);
    }

    public async Task CreateMatchFullNotificationsAsync(Guid matchId, IDbTransaction transaction, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT user_id
            FROM matching.match_participants
            WHERE match_id = @MatchId
              AND status = 'ACTIVE';
            """;

        var participants = (await transaction.Connection!.QueryAsync<Guid>(
            new CommandDefinition(sql, new { MatchId = matchId }, transaction: transaction, cancellationToken: cancellationToken))).AsList();

        foreach (var participantUserId in participants)
        {
            await CreateNotificationAsync(
                participantUserId,
                matchId,
                "MATCH_FULL",
                "Match is full",
                "This match is now full and no new players can join.",
                transaction,
                cancellationToken);
        }
    }

    private async Task<T> ScalarAsync<T>(string sql, object parameters, CancellationToken cancellationToken)
    {
        using var connection = sqlConnectionFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<T>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
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

    private sealed class PlayerMatchDetailRow
    {
        public Guid Id { get; init; }
        public string Title { get; init; } = string.Empty;
        public string? Description { get; init; }
        public Guid BranchId { get; init; }
        public string BranchName { get; init; } = string.Empty;
        public string City { get; init; } = string.Empty;
        public string District { get; init; } = string.Empty;
        public string? CourtName { get; init; }
        public DateOnly MatchDate { get; init; }
        public TimeOnly StartTime { get; init; }
        public TimeOnly EndTime { get; init; }
        public string SkillLevel { get; init; } = string.Empty;
        public string? GenderPreference { get; init; }
        public int MaxPlayers { get; init; }
        public int CurrentPlayers { get; init; }
        public decimal? FeePerPlayer { get; init; }
        public string Status { get; init; } = string.Empty;
        public Guid CreatedBy { get; init; }
        public string CreatedByName { get; init; } = string.Empty;
        public DateTime CreatedAt { get; init; }
    }

    private sealed class PlayerMatchParticipantRow
    {
        public Guid UserId { get; init; }
        public string FullName { get; init; } = string.Empty;
        public string Role { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
        public DateTime JoinedAt { get; init; }
    }
}
