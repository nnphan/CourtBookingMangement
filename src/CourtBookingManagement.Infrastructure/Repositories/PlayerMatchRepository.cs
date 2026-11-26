using System.Data;
using System.Text;
using CourtBookingManagement.Application.Abstractions.Data;
using CourtBookingManagement.Application.Common.Constants;
using CourtBookingManagement.Application.Matching.Models;
using CourtBookingManagement.Application.Matching.Models.Requests;
using CourtBookingManagement.Application.Matching.Models.Responses;
using CourtBookingManagement.Application.Matching.Repositories;
using Dapper;
using Microsoft.Extensions.Logging;

namespace CourtBookingManagement.Infrastructure.Repositories;

public sealed class PlayerMatchRepository(ISqlConnectionFactory sqlConnectionFactory) : IPlayerMatchRepository
{
    private const int InitialPlayerCount = 1;

    public async Task<PlayerMatchDetailResponse?> GetByIdAsync(
        Guid matchId,
        Guid? viewerUserId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                pm.id AS Id,
                pm.title AS Title,
                pm.description AS Description,
                pm.branch_id AS BranchId,
                pm.booking_id AS BookingId,
                pm.room_code AS RoomCode,
                b.name AS BranchName,
                b.city AS City,
                b.district AS District,
                c.name AS CourtName,
                pm.match_date AS MatchDate,
                pm.start_time AS StartTime,
                pm.end_time AS EndTime,
                pm.skill_level AS SkillLevel,
                pm.gender_preference AS GenderPreference,
                pm.game_type AS GameType,
                pm.minimum_rating AS MinimumRating,
                pm.maximum_rating AS MaximumRating,
                pm.gender_policy AS GenderPolicy,
                pm.required_gender AS RequiredGender,
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
            WHERE pm.id = @MatchId
              AND (pm.status <> 'DRAFT' OR pm.created_by = @ViewerUserId);

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
            new CommandDefinition(sql, new { MatchId = matchId, ViewerUserId = viewerUserId }, cancellationToken: cancellationToken));

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
            BookingId = match.BookingId,
            RoomCode = match.RoomCode,
            BranchName = match.BranchName,
            City = match.City,
            District = match.District,
            CourtName = match.CourtName,
            MatchDate = match.MatchDate,
            StartTime = match.StartTime,
            EndTime = match.EndTime,
            SkillLevel = match.SkillLevel,
            GenderPreference = match.GenderPreference,
            GameType = match.GameType,
            MinimumRating = match.MinimumRating,
            MaximumRating = match.MaximumRating,
            GenderPolicy = match.GenderPolicy,
            RequiredGender = match.RequiredGender,
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

    public async Task<bool> BranchesExistAsync(
        IReadOnlyList<Guid> branchIds,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COUNT(*)
            FROM core.branches
            WHERE id = ANY(@BranchIds)
              AND is_active
              AND deleted_at IS NULL;
            """;

        using var connection = sqlConnectionFactory.CreateConnection();
        var count = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, new { BranchIds = branchIds.ToArray() }, cancellationToken: cancellationToken));

        return count == branchIds.Count;
    }

    public async Task<IReadOnlyList<PersonalizedMatchCandidate>> SearchPersonalizedCandidatesAsync(
        Guid viewerUserId,
        int? currentRating,
        string? viewerGender,
        PlayerMatchScoringCriteriaRequest criteria,
        Guid? matchId,
        int candidateLimit,
        CancellationToken cancellationToken)
    {
        const string sql = """
            WITH active_player_counts AS (
                SELECT match_id, COUNT(*)::integer AS current_player_count
                FROM matching.match_participants
                WHERE status = 'ACTIVE'
                GROUP BY match_id
            )
            SELECT
                pm.id AS RoomId,
                pm.room_code AS RoomCode,
                pm.title AS Title,
                pm.branch_id AS BranchId,
                br.name AS BranchName,
                COALESCE(NULLIF(court.name, ''), court.court_number::text) AS CourtName,
                pm.match_date AS MatchDate,
                pm.start_time AS StartTime,
                pm.end_time AS EndTime,
                pm.game_type AS GameType,
                pm.minimum_rating AS MinimumRating,
                pm.maximum_rating AS MaximumRating,
                COALESCE(active.current_player_count, 0) AS CurrentPlayerCount,
                pm.max_players AS RequiredPlayerCount,
                COALESCE(pm.fee_per_player, 0)::numeric AS ParticipationFee,
                pm.skill_level AS SkillLevel,
                pm.gender_preference AS GenderPreference,
                pm.gender_policy AS GenderPolicy,
                pm.required_gender AS RequiredGender,
                pm.created_at AS CreatedAt
            FROM matching.player_matches pm
            INNER JOIN core.branches br ON br.id = pm.branch_id
            INNER JOIN core.courts court ON court.id = pm.court_id
            INNER JOIN booking.bookings bk ON bk.id = pm.booking_id
            INNER JOIN customer.customers cu ON cu.id = bk.customer_id
            LEFT JOIN active_player_counts active ON active.match_id = pm.id
            WHERE (CAST(@MatchId AS uuid) IS NULL OR pm.id = @MatchId)
              AND pm.status = 'OPEN'
              AND pm.visibility = 'PUBLIC'
              AND pm.created_by <> @ViewerUserId
              AND (cardinality(CAST(@BranchIds AS uuid[])) = 0 OR pm.branch_id = ANY(@BranchIds))
              AND (CAST(@Keyword AS text) IS NULL OR pm.title ILIKE @Keyword OR br.name ILIKE @Keyword
                   OR COALESCE(NULLIF(court.name, ''), court.court_number::text) ILIKE @Keyword)
              AND (CAST(@GenderPreference AS text) IS NULL
                   OR UPPER(pm.gender_preference) = @GenderPreference
                   OR (UPPER(pm.gender_preference) = 'BOTH' AND @GenderPreference IN ('MALE', 'FEMALE')))
              AND pm.start_time < pm.end_time
              AND pm.game_type = ANY(@GameTypes)
              AND pm.minimum_rating IS NOT NULL AND pm.maximum_rating IS NOT NULL
              AND (CAST(@CurrentRating AS integer) IS NULL OR @CurrentRating BETWEEN pm.minimum_rating AND pm.maximum_rating)
              AND (
                  pm.gender_policy = 'ANY'
                  OR (
                      pm.gender_policy = 'SPECIFIC'
                      AND CAST(@ViewerGender AS text) IS NOT NULL
                      AND pm.required_gender = @ViewerGender
                  )
              )
              AND (CAST(@MaximumParticipationFee AS numeric) IS NULL
                   OR COALESCE(pm.fee_per_player, 0) <= @MaximumParticipationFee)
              AND COALESCE(active.current_player_count, 0) < pm.max_players
              AND active.current_player_count = pm.current_players
              AND br.is_active
              AND br.deleted_at IS NULL
              AND court.branch_id = pm.branch_id
              AND court.is_active
              AND court.deleted_at IS NULL
              AND UPPER(bk.status) = 'CONFIRMED'
              AND bk.is_active
              AND bk.deleted_at IS NULL
              AND bk.branch_id = pm.branch_id
              AND cu.user_id = pm.created_by
              AND cu.is_active
              AND cu.deleted_at IS NULL
              AND ((pm.match_date + pm.start_time)
                    AT TIME ZONE COALESCE(NULLIF(br.time_zone, ''), 'Asia/Ho_Chi_Minh')) > NOW()
              AND EXISTS (
                  SELECT 1
                  FROM booking.booking_details bd
                  WHERE bd.booking_id = bk.id
                    AND bd.court_id = pm.court_id
                    AND bd.booking_date = pm.match_date
                    AND bd.start_time <= pm.start_time
                    AND bd.end_time >= pm.end_time
                    AND UPPER(bd.status) = 'CONFIRMED'
              )
              AND NOT EXISTS (
                  SELECT 1 FROM matching.player_profiles profile
                  WHERE profile.user_id = @ViewerUserId AND NOT profile.is_matching_enabled
              )
              AND NOT EXISTS (
                  SELECT 1
                  FROM matching.match_participants participant
                  WHERE participant.match_id = pm.id
                    AND participant.user_id = @ViewerUserId
                    AND participant.status = 'ACTIVE'
              )
              AND NOT EXISTS (
                  SELECT 1
                  FROM matching.match_join_requests join_request
                  WHERE join_request.match_id = pm.id
                    AND join_request.requester_id = @ViewerUserId
                    AND join_request.status = 'PENDING'
              )
              AND (CAST(@MatchDate AS date) IS NULL OR pm.match_date = @MatchDate)
            ORDER BY pm.match_date ASC, pm.start_time ASC, pm.id ASC
            LIMIT @CandidateLimit;
            """;

        using var connection = sqlConnectionFactory.CreateConnection();
        return (await connection.QueryAsync<PersonalizedMatchCandidate>(
            new CommandDefinition(sql, new
            {
                ViewerUserId = viewerUserId,
                CurrentRating = currentRating,
                ViewerGender = viewerGender,
                BranchIds = criteria.BranchIds.ToArray(),
                Keyword = criteria.Keyword is null ? null : $"%{criteria.Keyword}%",
                criteria.GenderPreference,
                MatchDate = criteria.MatchDate,
                GameTypes = criteria.GameTypes.ToArray(),
                MaximumParticipationFee = criteria.MaximumParticipationFee,
                MatchId = matchId,
                CandidateLimit = candidateLimit
            }, cancellationToken: cancellationToken))).AsList();
    }

    public Task<PlayerMatchDetailResponse?> GetMatchByIdAsync(Guid matchId, CancellationToken cancellationToken) =>
        GetByIdAsync(matchId, null, cancellationToken);

    public async Task<PlayerMatchLifecycleContext?> LockMatchForLifecycleAsync(
        Guid matchId,
        IDbTransaction transaction,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                id AS Id,
                created_by AS CreatedBy,
                branch_id AS BranchId,
                court_id AS CourtId,
                booking_id AS BookingId,
                match_date AS MatchDate,
                start_time AS StartTime,
                end_time AS EndTime,
                max_players AS MaximumPlayers,
                current_players AS CurrentPlayers,
                game_type AS GameType,
                minimum_rating AS MinimumRating,
                maximum_rating AS MaximumRating,
                gender_policy AS GenderPolicy,
                required_gender AS RequiredGender,
                fee_per_player AS ParticipationFee,
                status AS Status
            FROM matching.player_matches
            WHERE id = @MatchId
            FOR UPDATE;
            """;

        return await transaction.Connection!.QuerySingleOrDefaultAsync<PlayerMatchLifecycleContext>(
            new CommandDefinition(sql, new { MatchId = matchId }, transaction, cancellationToken: cancellationToken));
    }

    public async Task<PlayerMatchEligibilityContext?> LockMatchForApprovalAsync(
        Guid matchId,
        IDbTransaction transaction,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                pm.id AS MatchId,
                pm.created_by AS HostUserId,
                pm.status AS Status,
                pm.booking_id AS BookingId,
                pm.branch_id AS BranchId,
                pm.court_id AS CourtId,
                pm.match_date AS MatchDate,
                pm.start_time AS StartTime,
                pm.end_time AS EndTime,
                pm.current_players AS CurrentPlayers,
                pm.max_players AS MaximumPlayers,
                pm.game_type AS GameType,
                pm.minimum_rating AS MinimumRating,
                pm.maximum_rating AS MaximumRating,
                pm.gender_policy AS GenderPolicy,
                pm.required_gender AS RequiredGender,
                COALESCE(
                    ((pm.match_date + pm.start_time)
                        AT TIME ZONE COALESCE(NULLIF(branch.time_zone, ''), 'Asia/Ho_Chi_Minh')) > NOW(),
                    FALSE
                ) AS HasNotStarted
            FROM matching.player_matches pm
            INNER JOIN core.branches branch ON branch.id = pm.branch_id
            WHERE pm.id = @MatchId
            FOR UPDATE OF pm;
            """;

        return await transaction.Connection!.QuerySingleOrDefaultAsync<PlayerMatchEligibilityContext>(
            new CommandDefinition(sql, new { MatchId = matchId }, transaction, cancellationToken: cancellationToken));
    }

    public Task<PlayerMatchEligibilityContext?> LockMatchForJoinAsync(
        Guid matchId,
        IDbTransaction transaction,
        CancellationToken cancellationToken) =>
        LockMatchForApprovalAsync(matchId, transaction, cancellationToken);

    public async Task<PlayerMatchJoinRequest?> LockJoinRequestForApprovalAsync(
        Guid matchId,
        Guid requestId,
        IDbTransaction transaction,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                id AS Id,
                match_id AS MatchId,
                requester_id AS UserId,
                status AS Status,
                reviewed_at AS ReviewedAt,
                reviewed_by AS ReviewedBy,
                note AS Message,
                rejection_reason AS RejectionReason
            FROM matching.match_join_requests
            WHERE match_id = @MatchId
              AND id = @RequestId
            FOR UPDATE;
            """;

        return await transaction.Connection!.QuerySingleOrDefaultAsync<PlayerMatchJoinRequest>(
            new CommandDefinition(sql, new { MatchId = matchId, RequestId = requestId }, transaction, cancellationToken: cancellationToken));
    }

    public async Task<int> CountActiveParticipantsAsync(
        Guid matchId,
        IDbTransaction transaction,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COUNT(*)::integer
            FROM matching.match_participants
            WHERE match_id = @MatchId
              AND status = 'ACTIVE';
            """;

        return await transaction.Connection!.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, new { MatchId = matchId }, transaction, cancellationToken: cancellationToken));
    }

    public async Task<bool> IsBookingValidAsync(
        PlayerMatchEligibilityContext match,
        IDbTransaction transaction,
        CancellationToken cancellationToken)
    {
        if (match.BookingId is null || match.CourtId is null
            || match.MatchDate is null || match.StartTime is null || match.EndTime is null)
        {
            return false;
        }

        const string sql = """
            SELECT TRUE
            FROM booking.bookings b
            INNER JOIN customer.customers cu ON cu.id = b.customer_id
            INNER JOIN booking.booking_details bd ON bd.booking_id = b.id
            INNER JOIN core.courts c ON c.id = bd.court_id
            INNER JOIN core.branches br ON br.id = b.branch_id
            WHERE b.id = @BookingId
              AND b.branch_id = @BranchId
              AND UPPER(b.status) = 'CONFIRMED'
              AND b.is_active
              AND b.deleted_at IS NULL
              AND cu.user_id = @HostUserId
              AND cu.is_active
              AND cu.deleted_at IS NULL
              AND bd.court_id = @CourtId
              AND bd.booking_date = @MatchDate
              AND bd.start_time <= @StartTime
              AND bd.end_time >= @EndTime
              AND UPPER(bd.status) = 'CONFIRMED'
              AND c.branch_id = @BranchId
              AND c.is_active
              AND c.deleted_at IS NULL
              AND br.is_active
              AND br.deleted_at IS NULL
            LIMIT 1
            FOR SHARE OF b, cu, bd, c, br;
            """;

        return await transaction.Connection!.QuerySingleOrDefaultAsync<bool>(
            new CommandDefinition(sql, new
            {
                match.BookingId,
                match.BranchId,
                match.HostUserId,
                match.CourtId,
                match.MatchDate,
                match.StartTime,
                match.EndTime
            }, transaction, cancellationToken: cancellationToken));
    }

    public async Task<bool> IsPlayerEligibleForApprovalAsync(
        PlayerMatchEligibilityContext match,
        Guid userId,
        IDbTransaction transaction,
        CancellationToken cancellationToken)
    {
        if (match.MinimumRating is null || match.MaximumRating is null || match.GameType is null)
        {
            return false;
        }

        const string sql = """
            SELECT profile.id
            FROM matching.player_profiles profile
            WHERE profile.user_id = @UserId
              AND profile.is_matching_enabled
              AND profile.current_rating BETWEEN @MinimumRating AND @MaximumRating
              AND (
                  COALESCE(@GenderPolicy, 'ANY') = 'ANY'
                  OR (
                      @GenderPolicy = 'SPECIFIC'
                      AND @RequiredGender IS NOT NULL
                      AND profile.gender = @RequiredGender
                  )
              )
            LIMIT 1
            FOR SHARE OF profile;
            """;

        var profileId = await transaction.Connection!.QuerySingleOrDefaultAsync<Guid?>(
            new CommandDefinition(sql, new
            {
                UserId = userId,
                match.MinimumRating,
                match.MaximumRating,
                match.GenderPolicy,
                match.RequiredGender
            }, transaction, cancellationToken: cancellationToken));

        return profileId is not null;
    }

    public async Task<bool> IsActiveParticipantAsync(
        Guid matchId,
        Guid userId,
        IDbTransaction transaction,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT EXISTS (
                SELECT 1
                FROM matching.match_participants
                WHERE match_id = @MatchId
                  AND user_id = @UserId
                  AND status = 'ACTIVE'
            );
            """;

        return await transaction.Connection!.ExecuteScalarAsync<bool>(
            new CommandDefinition(sql, new { MatchId = matchId, UserId = userId }, transaction, cancellationToken: cancellationToken));
    }

    public async Task<PlayerMatchJoinRequest?> GetJoinRequestAsync(Guid requestId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                id AS Id,
                match_id AS MatchId,
                requester_id AS UserId,
                status AS Status,
                reviewed_at AS ReviewedAt,
                reviewed_by AS ReviewedBy,
                note AS Message,
                rejection_reason AS RejectionReason
            FROM matching.match_join_requests
            WHERE id = @RequestId;
            """;

        using var connection = sqlConnectionFactory.CreateConnection();

        return await connection.QuerySingleOrDefaultAsync<PlayerMatchJoinRequest>(
            new CommandDefinition(sql, new { RequestId = requestId }, cancellationToken: cancellationToken));
    }

    public async Task<bool> RejectJoinRequestAsync(
        Guid matchId,
        Guid requestId,
        Guid currentUserId,
        string? reason,
        IDbTransaction transaction,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE matching.match_join_requests AS jr
            SET status = 'REJECTED',
                reviewed_at = NOW(),
                reviewed_by = @CurrentUserId,
                rejection_reason = @Reason,
                updated_at = NOW()
            WHERE jr.id = @RequestId
              AND jr.match_id = @MatchId
              AND jr.status = 'PENDING'
              AND EXISTS (
                  SELECT 1
                  FROM matching.player_matches AS pm
                  WHERE pm.id = @MatchId
                    AND pm.created_by = @CurrentUserId
                    AND pm.status = 'OPEN'
              );
            """;

        var command = new CommandDefinition(sql, new
        {
            MatchId = matchId,
            RequestId = requestId,
            CurrentUserId = currentUserId,
            Reason = reason
        }, transaction: transaction, cancellationToken: cancellationToken);
        return await transaction.Connection!.ExecuteAsync(command) == 1;
    }

    public async Task<Guid?> CancelJoinRequestAsync(Guid matchId, Guid userId, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE matching.match_join_requests
            SET status = 'CANCELLED',
                updated_at = NOW()
            WHERE match_id = @MatchId
              AND requester_id = @UserId
              AND status = 'PENDING'
            RETURNING id;
            """;

        using var connection = sqlConnectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<Guid?>(
            new CommandDefinition(sql, new { MatchId = matchId, UserId = userId }, cancellationToken: cancellationToken));
    }

    public async Task ExpirePendingJoinRequestsAsync(
        Guid matchId,
        IDbTransaction transaction,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE matching.match_join_requests
            SET status = 'EXPIRED',
                updated_at = NOW()
            WHERE match_id = @MatchId
              AND status = 'PENDING';
            """;

        await transaction.Connection!.ExecuteAsync(
            new CommandDefinition(sql, new { MatchId = matchId }, transaction, cancellationToken: cancellationToken));
    }

    public Task<bool> IsParticipantAsync(Guid matchId, Guid userId, CancellationToken cancellationToken) =>
        ScalarAsync<bool>(
            "SELECT EXISTS (SELECT 1 FROM matching.match_participants WHERE match_id = @MatchId AND user_id = @UserId AND status = 'ACTIVE');",
            new { MatchId = matchId, UserId = userId },
            cancellationToken);

    public Task<bool> HasPendingJoinRequestAsync(Guid matchId, Guid userId, CancellationToken cancellationToken) =>
        ScalarAsync<bool>(
            "SELECT EXISTS (SELECT 1 FROM matching.match_join_requests WHERE match_id = @MatchId AND requester_id = @UserId AND status = 'PENDING');",
            new { MatchId = matchId, UserId = userId },
            cancellationToken);

    public async Task<bool> HasPendingJoinRequestAsync(
        Guid matchId,
        Guid userId,
        IDbTransaction transaction,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT EXISTS (
                SELECT 1
                FROM matching.match_join_requests
                WHERE match_id = @MatchId
                  AND requester_id = @UserId
                  AND status = 'PENDING'
            );
            """;

        return await transaction.Connection!.ExecuteScalarAsync<bool>(
            new CommandDefinition(sql, new { MatchId = matchId, UserId = userId }, transaction, cancellationToken: cancellationToken));
    }

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
                  AND pm.status IN ('OPEN', 'FULL', 'STARTED')
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
                  AND pm.status IN ('OPEN', 'FULL', 'STARTED')
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
                booking_id,
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
                @BookingId,
                @Title,
                @Description,
                @MatchDate,
                @StartTime,
                @EndTime,
                @SkillLevel,
                @GenderPreference,
                @MaxPlayers,
                @InitialPlayerCount,
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
            BookingId = request.BookingId,
            Title = request.Title.Trim(),
            Description = request.Description,
            MatchDate = request.MatchDate,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            SkillLevel = request.SkillLevel,
            GenderPreference = request.GenderPreference,
            MaxPlayers = request.MaxPlayers,
            InitialPlayerCount,
            FeePerPlayer = request.FeePerPlayer
        }, transaction: transaction, cancellationToken: cancellationToken);

        return await transaction.Connection!.QuerySingleAsync<Guid>(command);
    }

    public async Task<Guid> CreateDraftAsync(
        PlayerMatchDraftRequest request,
        Guid createdBy,
        IDbTransaction transaction,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO matching.player_matches (
                id,
                created_by,
                branch_id,
                court_id,
                booking_id,
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
                game_type,
                minimum_rating,
                maximum_rating,
                gender_policy,
                required_gender,
                approval_mode,
                visibility,
                created_at,
                updated_at
            )
            VALUES (
                uuid_generate_v7(),
                @CreatedBy,
                @BranchId,
                @CourtId,
                @BookingId,
                @Title,
                @Description,
                @MatchDate,
                @StartTime,
                @EndTime,
                @SkillLevel,
                @GenderPreference,
                @RequiredPlayerCount,
                1,
                @ParticipationFee,
                'DRAFT',
                @GameType,
                @MinimumRating,
                @MaximumRating,
                @GenderPolicy,
                @RequiredGender,
                'MANUAL',
                'PUBLIC',
                NOW(),
                NOW()
            )
            RETURNING id;
            """;

        return await transaction.Connection!.QuerySingleAsync<Guid>(
            new CommandDefinition(sql, DraftParameters(request, createdBy), transaction, cancellationToken: cancellationToken));
    }

    public async Task<bool> UpdateDraftAsync(
        Guid matchId,
        PlayerMatchDraftRequest request,
        Guid currentUserId,
        IDbTransaction transaction,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE matching.player_matches
            SET branch_id = @BranchId,
                court_id = @CourtId,
                booking_id = @BookingId,
                title = @Title,
                description = @Description,
                match_date = @MatchDate,
                start_time = @StartTime,
                end_time = @EndTime,
                skill_level = @SkillLevel,
                gender_preference = @GenderPreference,
                max_players = @RequiredPlayerCount,
                fee_per_player = @ParticipationFee,
                game_type = @GameType,
                minimum_rating = @MinimumRating,
                maximum_rating = @MaximumRating,
                gender_policy = @GenderPolicy,
                required_gender = @RequiredGender,
                updated_at = NOW()
            WHERE id = @MatchId
              AND created_by = @CurrentUserId
              AND status = 'DRAFT';
            """;

        return await transaction.Connection!.ExecuteAsync(
            new CommandDefinition(
                sql,
                DraftParameters(request, currentUserId, matchId),
                transaction,
                cancellationToken: cancellationToken)) == 1;
    }

    public async Task<bool> IsBookingValidForPublicationAsync(
        PlayerMatchLifecycleContext match,
        IDbTransaction transaction,
        CancellationToken cancellationToken)
    {
        if (match.BookingId is not Guid bookingId
            || match.CourtId is not Guid courtId
            || match.MatchDate is not DateOnly matchDate
            || match.StartTime is not TimeOnly startTime
            || match.EndTime is not TimeOnly endTime)
        {
            return false;
        }

        const string sql = """
            SELECT TRUE
            FROM booking.bookings b
            INNER JOIN customer.customers cu ON cu.id = b.customer_id
            INNER JOIN booking.booking_details bd ON bd.booking_id = b.id
            INNER JOIN core.courts c ON c.id = bd.court_id
            INNER JOIN core.branches br ON br.id = b.branch_id
            WHERE b.id = @BookingId
              AND b.branch_id = @BranchId
              AND UPPER(b.status) = 'CONFIRMED'
              AND b.is_active
              AND b.deleted_at IS NULL
              AND cu.user_id = @CreatedBy
              AND cu.is_active
              AND cu.deleted_at IS NULL
              AND bd.court_id = @CourtId
              AND bd.booking_date = @MatchDate
              AND bd.start_time <= @StartTime
              AND bd.end_time >= @EndTime
              AND UPPER(bd.status) = 'CONFIRMED'
              AND c.branch_id = @BranchId
              AND c.is_active
              AND c.deleted_at IS NULL
              AND br.is_active
              AND br.deleted_at IS NULL
            LIMIT 1
            FOR UPDATE OF b, cu, bd, c, br;
            """;

        var exists = await transaction.Connection!.QuerySingleOrDefaultAsync<bool?>(
            new CommandDefinition(sql, new
            {
                BookingId = bookingId,
                BranchId = match.BranchId,
                CreatedBy = match.CreatedBy,
                CourtId = courtId,
                MatchDate = matchDate,
                StartTime = startTime,
                EndTime = endTime
            }, transaction, cancellationToken: cancellationToken));

        return exists.GetValueOrDefault();
    }

    public async Task<bool> HasOverlappingPublishedMatchAsync(
        PlayerMatchLifecycleContext match,
        IDbTransaction transaction,
        CancellationToken cancellationToken)
    {
        if (match.CourtId is not Guid courtId
            || match.MatchDate is not DateOnly matchDate
            || match.StartTime is not TimeOnly startTime
            || match.EndTime is not TimeOnly endTime)
        {
            return false;
        }

        const string sql = """
            SELECT EXISTS (
                SELECT 1
                FROM matching.player_matches pm
                WHERE pm.id <> @MatchId
                  AND pm.match_date = @MatchDate
                  AND pm.status IN ('OPEN', 'FULL', 'STARTED')
                  AND (pm.court_id = @CourtId OR pm.created_by = @CreatedBy)
                  AND @StartTime < pm.end_time
                  AND @EndTime > pm.start_time
            );
            """;

        return await transaction.Connection!.ExecuteScalarAsync<bool>(
            new CommandDefinition(sql, new
            {
                MatchId = match.Id,
                CreatedBy = match.CreatedBy,
                CourtId = courtId,
                MatchDate = matchDate,
                StartTime = startTime,
                EndTime = endTime
            }, transaction, cancellationToken: cancellationToken));
    }

    public async Task<bool> PublishDraftAsync(
        Guid matchId,
        Guid currentUserId,
        IDbTransaction transaction,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE matching.player_matches
            SET status = 'OPEN',
                updated_at = NOW()
            WHERE id = @MatchId
              AND created_by = @CurrentUserId
              AND status = 'DRAFT';
            """;

        return await transaction.Connection!.ExecuteAsync(
            new CommandDefinition(sql, new { MatchId = matchId, CurrentUserId = currentUserId }, transaction, cancellationToken: cancellationToken)) == 1;
    }

    public async Task<bool> CancelMatchAsync(
        Guid matchId,
        Guid currentUserId,
        IDbTransaction transaction,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE matching.player_matches
            SET status = 'CANCELLED',
                cancelled_at = NOW(),
                cancelled_by = @CurrentUserId,
                updated_at = NOW()
            WHERE id = @MatchId
              AND created_by = @CurrentUserId
              AND status IN ('DRAFT', 'OPEN', 'FULL');
            """;

        return await transaction.Connection!.ExecuteAsync(
            new CommandDefinition(sql, new { MatchId = matchId, CurrentUserId = currentUserId }, transaction, cancellationToken: cancellationToken)) == 1;
    }

    public async Task<bool> CancelInvalidatedMatchAsync(
        Guid matchId,
        IDbTransaction transaction,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE matching.player_matches
            SET status = 'CANCELLED',
                cancelled_at = NOW(),
                cancelled_by = NULL,
                updated_at = NOW()
            WHERE id = @MatchId
              AND status IN ('OPEN', 'FULL', 'STARTED');
            """;

        return await transaction.Connection!.ExecuteAsync(
            new CommandDefinition(sql, new { MatchId = matchId }, transaction, cancellationToken: cancellationToken)) == 1;
    }

    public async Task<Guid?> CreateJoinRequestAsync(
        Guid matchId,
        Guid userId,
        string? message,
        IDbTransaction transaction,
        CancellationToken cancellationToken)
    {
        const string sql = """
            WITH available_match AS (
                SELECT pm.id
                FROM matching.player_matches pm
                INNER JOIN core.branches br ON br.id = pm.branch_id
                WHERE pm.id = @MatchId
                  AND pm.created_by <> @UserId
                  AND pm.status = 'OPEN'
                  AND pm.current_players < pm.max_players
                  AND COALESCE(
                      ((pm.match_date + pm.start_time)
                          AT TIME ZONE COALESCE(NULLIF(br.time_zone, ''), 'Asia/Ho_Chi_Minh')) > NOW(),
                      FALSE
                  )
                FOR UPDATE OF pm
            )
            INSERT INTO matching.match_join_requests (
                id,
                match_id,
                requester_id,
                note,
                status,
                requested_at,
                reviewed_at,
                reviewed_by
            )
            SELECT
                uuid_generate_v7(),
                @MatchId,
                @UserId,
                @Message,
                'PENDING',
                NOW(),
                NULL,
                NULL
            FROM available_match
            WHERE NOT EXISTS (
                SELECT 1
                FROM matching.match_participants mp
                WHERE mp.match_id = @MatchId
                  AND mp.user_id = @UserId
                  AND mp.status = 'ACTIVE'
            )
            ON CONFLICT (match_id, requester_id) WHERE status = 'PENDING' DO NOTHING
            RETURNING id;
            """;

        var command = new CommandDefinition(sql, new { MatchId = matchId, UserId = userId, Message = message }, transaction: transaction, cancellationToken: cancellationToken);
        return await transaction.Connection!.QuerySingleOrDefaultAsync<Guid?>(command);
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

    public async Task<bool> ApproveJoinRequestAsync(Guid matchId, Guid requestId, Guid currentUserId, IDbTransaction transaction, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE matching.match_join_requests AS jr
            SET status = 'APPROVED',
                reviewed_at = NOW(),
                reviewed_by = @CurrentUserId,
                updated_at = NOW()
            WHERE jr.id = @RequestId
              AND jr.match_id = @MatchId
              AND jr.status = 'PENDING'
              AND EXISTS (
                  SELECT 1
                  FROM matching.player_matches pm
                  WHERE pm.id = @MatchId
                    AND pm.created_by = @CurrentUserId
                    AND pm.status = 'OPEN'
              );
            """;

        var command = new CommandDefinition(sql, new { MatchId = matchId, RequestId = requestId, CurrentUserId = currentUserId }, transaction: transaction, cancellationToken: cancellationToken);
        return await transaction.Connection!.ExecuteAsync(command) == 1;
    }

    public async Task<int?> TryIncrementCurrentPlayersAsync(Guid matchId, IDbTransaction transaction, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE matching.player_matches AS pm
            SET current_players = pm.current_players + 1,
                updated_at = NOW()
            WHERE pm.id = @MatchId
              AND pm.status = 'OPEN'
              AND pm.current_players < pm.max_players
              AND EXISTS (
                  SELECT 1
                  FROM core.branches branch
                  WHERE branch.id = pm.branch_id
                    AND COALESCE(
                        ((pm.match_date + pm.start_time)
                            AT TIME ZONE COALESCE(NULLIF(branch.time_zone, ''), 'Asia/Ho_Chi_Minh')) > NOW(),
                        FALSE
                    )
              )
            RETURNING pm.current_players;
            """;

        var command = new CommandDefinition(sql, new { MatchId = matchId }, transaction: transaction, cancellationToken: cancellationToken);
        return await transaction.Connection!.QuerySingleOrDefaultAsync<int?>(command);
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

    public async Task CreateNotificationAsync(
        Guid userId,
        Guid? matchId,
        string type,
        string title,
        string message,
        CancellationToken cancellationToken)
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

        using var connection = sqlConnectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new { UserId = userId, MatchId = matchId, Type = type, Title = title, Message = message },
            cancellationToken: cancellationToken));
    }

    public async Task CreateMatchFullNotificationsAsync(Guid matchId, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO matching.match_notifications (
                id, user_id, match_id, type, title, message, is_read, created_at, read_at
            )
            SELECT
                uuid_generate_v7(),
                participant.user_id,
                @MatchId,
                'MATCH_FULL',
                'Match is full',
                'This match is now full and no new players can join.',
                FALSE,
                NOW(),
                NULL
            FROM matching.match_participants participant
            WHERE participant.match_id = @MatchId
              AND participant.status = 'ACTIVE';
            """;

        using var connection = sqlConnectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            sql, new { MatchId = matchId }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<HostedPlayerMatchResponse>> GetHostedMatchesAsync(
        Guid userId,
        GetHostedMatchesRequest request,
        CancellationToken cancellationToken)
    {
        const string baseQuery = """
            WITH hosted AS (
                SELECT
                    pm.id AS Id,
                    pm.title AS Title,
                    pm.branch_id AS BranchId,
                    b.name AS BranchName,
                    pm.court_id AS CourtId,
                    c.name AS CourtName,
                    pm.match_date AS MatchDate,
                    pm.start_time AS StartTime,
                    pm.end_time AS EndTime,
                    pm.skill_level AS SkillLevel,
                    pm.gender_preference AS GenderPreference,
                    pm.max_players AS MaxPlayers,
                    pm.current_players AS CurrentPlayers,
                    GREATEST(pm.max_players - pm.current_players, 0) AS RemainingSlots,
                    pm.fee_per_player AS FeePerPlayer,
                    pm.status AS Status,
                    (
                        SELECT COUNT(*)::int
                        FROM matching.match_join_requests jr
                        WHERE jr.match_id = pm.id
                          AND jr.status = 'PENDING'
                    ) AS PendingRequestCount,
                    (pm.current_players >= pm.max_players) AS IsFull
                FROM matching.player_matches pm
                INNER JOIN core.branches b ON b.id = pm.branch_id
                LEFT JOIN core.courts c ON c.id = pm.court_id
                WHERE pm.created_by = @UserId
            )
            """;

        var filters = new StringBuilder();
        var parameters = new DynamicParameters();
        parameters.Add("UserId", userId);

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            filters.AppendLine("AND (Title ILIKE @Keyword OR BranchName ILIKE @Keyword OR CourtName ILIKE @Keyword)");
            parameters.Add("Keyword", $"%{request.Keyword.Trim()}%");
        }

        if (request.MatchDate.HasValue)
        {
            filters.AppendLine("AND MatchDate = @MatchDate");
            parameters.Add("MatchDate", request.MatchDate.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            filters.AppendLine("AND Status = @Status");
            parameters.Add("Status", request.Status);
        }

        var dataSql = $"""
            {baseQuery}
            SELECT * FROM hosted WHERE 1 = 1
            {filters}
            ORDER BY MatchDate ASC, StartTime ASC, Id ASC
            LIMIT @PageSize OFFSET @Offset;
            """;

        var countSql = $"""
            {baseQuery}
            SELECT COUNT(*) FROM hosted WHERE 1 = 1
            {filters};
            """;

        parameters.Add("PageSize", request.PageSize);
        parameters.Add("Offset", ((long)request.Page - 1) * request.PageSize);

        using var connection = sqlConnectionFactory.CreateConnection();

        var items = (await connection.QueryAsync<HostedPlayerMatchResponse>(
            new CommandDefinition(dataSql, parameters, cancellationToken: cancellationToken))).AsList();

        var totalCount = await connection.ExecuteScalarAsync<long>(
            new CommandDefinition(countSql, parameters, cancellationToken: cancellationToken));

        return PagedResult<HostedPlayerMatchResponse>.Create(items, request.Page, request.PageSize, totalCount);
    }

    public async Task<Guid?> GetMatchHostIdAsync(Guid matchId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT created_by
            FROM matching.player_matches
            WHERE id = @MatchId;
            """;

        using var connection = sqlConnectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<Guid?>(
            new CommandDefinition(sql, new { MatchId = matchId }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<PlayerMatchJoinRequestResponse>> GetJoinRequestsAsync(
        Guid matchId,
        string? status,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                jr.id AS RequestId,
                jr.match_id AS MatchId,
                jr.requester_id AS UserId,
                u.full_name AS FullName,
                NULL::text AS SkillLevel,
                jr.status AS Status,
                jr.requested_at AS RequestedAt,
                jr.note AS Message,
                jr.rejection_reason AS RejectionReason
            FROM matching.match_join_requests jr
            INNER JOIN auth.users u ON u.id = jr.requester_id
            WHERE jr.match_id = @MatchId
              AND (@Status IS NULL OR jr.status = @Status)
            ORDER BY jr.requested_at ASC, jr.id ASC;
            """;

        using var connection = sqlConnectionFactory.CreateConnection();
        var requests = await connection.QueryAsync<PlayerMatchJoinRequestResponse>(
            new CommandDefinition(
                sql,
                new { MatchId = matchId, Status = status },
                cancellationToken: cancellationToken));

        return requests.AsList();
    }

    public async Task<PagedResult<MyMatchJoinRequestResponse>> GetMyJoinRequestsAsync(
        Guid userId,
        GetMyMatchJoinRequestsRequest request,
        CancellationToken cancellationToken)
    {
        const string fromAndWhereSql = """
            FROM matching.match_join_requests jr
            INNER JOIN matching.player_matches pm ON pm.id = jr.match_id
            INNER JOIN core.branches b ON b.id = pm.branch_id
            INNER JOIN auth.users host ON host.id = pm.created_by
            WHERE jr.requester_id = @UserId
              AND (@Status IS NULL OR jr.status = @Status)
            """;
        var dataSql = $"""
            SELECT
                jr.id AS RequestId,
                jr.match_id AS MatchId,
                pm.title AS MatchTitle,
                b.name AS BranchName,
                host.full_name AS HostName,
                jr.status AS Status,
                jr.note AS Message,
                jr.rejection_reason AS RejectionReason,
                jr.requested_at AS RequestedAt,
                jr.reviewed_at AS ReviewedAt
            {fromAndWhereSql}
            ORDER BY jr.requested_at DESC, jr.id DESC
            LIMIT @PageSize OFFSET @Offset;
            """;
        var countSql = $"SELECT COUNT(*) {fromAndWhereSql};";
        var parameters = new
        {
            UserId = userId,
            request.Status,
            request.PageSize,
            Offset = ((long)request.Page - 1) * request.PageSize
        };

        using var connection = sqlConnectionFactory.CreateConnection();
        var items = (await connection.QueryAsync<MyMatchJoinRequestResponse>(
            new CommandDefinition(dataSql, parameters, cancellationToken: cancellationToken))).AsList();
        var totalCount = await connection.ExecuteScalarAsync<long>(
            new CommandDefinition(countSql, parameters, cancellationToken: cancellationToken));

        return PagedResult<MyMatchJoinRequestResponse>.Create(items, request.Page, request.PageSize, totalCount);
    }

    public async Task<PagedResult<ParticipatingPlayerMatchResponse>> GetParticipatingMatchesAsync(
        Guid userId,
        GetParticipatingMatchesRequest request,
        CancellationToken cancellationToken)
    {
        // The latest request per match is shown; active membership takes precedence as APPROVED.
        const string baseQuery = """
            WITH participating AS (
                SELECT
                    pm.id AS MatchId,
                    pm.title AS Title,
                    pm.branch_id AS BranchId,
                    b.name AS BranchName,
                    pm.court_id AS CourtId,
                    c.name AS CourtName,
                    pm.match_date AS MatchDate,
                    pm.start_time AS StartTime,
                    pm.end_time AS EndTime,
                    pm.skill_level AS SkillLevel,
                    pm.gender_preference AS GenderPreference,
                    pm.max_players AS MaxPlayers,
                    pm.current_players AS CurrentPlayers,
                    GREATEST(pm.max_players - pm.current_players, 0) AS RemainingSlots,
                    pm.fee_per_player AS FeePerPlayer,
                    pm.status AS MatchStatus,
                    CASE
                        WHEN EXISTS (
                            SELECT 1
                            FROM matching.match_participants mp
                            WHERE mp.match_id = pm.id
                              AND mp.user_id = @UserId
                              AND mp.status = 'ACTIVE'
                        ) THEN 'APPROVED'
                        ELSE jr.status
                    END AS ParticipationStatus,
                    jr.id AS RequestId,
                    jr.requested_at AS RequestedAt,
                    u.full_name AS CreatedByName
                FROM (
                    SELECT DISTINCT ON (match_id) *
                    FROM matching.match_join_requests
                    WHERE requester_id = @UserId
                    ORDER BY match_id, requested_at DESC, id DESC
                ) jr
                INNER JOIN matching.player_matches pm ON pm.id = jr.match_id
                INNER JOIN core.branches b ON b.id = pm.branch_id
                LEFT JOIN core.courts c ON c.id = pm.court_id
                INNER JOIN auth.users u ON u.id = pm.created_by
                WHERE pm.created_by <> @UserId
            )
            """;

        var filters = new StringBuilder();
        var parameters = new DynamicParameters();
        parameters.Add("UserId", userId);

        if (!string.IsNullOrWhiteSpace(request.ParticipationStatus))
        {
            filters.AppendLine("AND ParticipationStatus = @ParticipationStatus");
            parameters.Add("ParticipationStatus", request.ParticipationStatus);
        }

        if (!string.IsNullOrWhiteSpace(request.MatchStatus))
        {
            filters.AppendLine("AND MatchStatus = @MatchStatus");
            parameters.Add("MatchStatus", request.MatchStatus);
        }

        if (request.FromDate.HasValue)
        {
            filters.AppendLine("AND MatchDate >= @FromDate");
            parameters.Add("FromDate", request.FromDate.Value);
        }

        var dataSql = $"""
            {baseQuery}
            SELECT
                MatchId, Title, BranchId, BranchName, CourtId, CourtName, MatchDate, StartTime, EndTime,
                SkillLevel, GenderPreference, MaxPlayers, CurrentPlayers, RemainingSlots, FeePerPlayer,
                MatchStatus, ParticipationStatus, RequestId, RequestedAt, CreatedByName,
                (ParticipationStatus = 'PENDING' AND MatchStatus = 'OPEN') AS CanCancelRequest
            FROM participating WHERE 1 = 1
            {filters}
            ORDER BY MatchDate ASC, StartTime ASC, MatchId ASC
            LIMIT @PageSize OFFSET @Offset;
            """;

        var countSql = $"""
            {baseQuery}
            SELECT COUNT(*) FROM participating WHERE 1 = 1
            {filters};
            """;

        parameters.Add("PageSize", request.PageSize);
        parameters.Add("Offset", ((long)request.Page - 1) * request.PageSize);

        using var connection = sqlConnectionFactory.CreateConnection();

        var items = (await connection.QueryAsync<ParticipatingPlayerMatchResponse>(
            new CommandDefinition(dataSql, parameters, cancellationToken: cancellationToken))).AsList();

        var totalCount = await connection.ExecuteScalarAsync<long>(
            new CommandDefinition(countSql, parameters, cancellationToken: cancellationToken));

        return PagedResult<ParticipatingPlayerMatchResponse>.Create(items, request.Page, request.PageSize, totalCount);
    }

    private async Task<T> ScalarAsync<T>(string sql, object parameters, CancellationToken cancellationToken)
    {
        using var connection = sqlConnectionFactory.CreateConnection();
        var result = await connection.ExecuteScalarAsync<T>(
            new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));

        return result is null
            ? throw new InvalidOperationException("The database query did not return a scalar value.")
            : result;
    }

    private static object DraftParameters(PlayerMatchDraftRequest request, Guid userId, Guid? matchId = null) =>
        new
        {
            CreatedBy = userId,
            CurrentUserId = userId,
            MatchId = matchId,
            BranchId = request.BranchId,
            CourtId = request.CourtId,
            BookingId = request.BookingId,
            Title = request.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            MatchDate = request.MatchDate,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            SkillLevel = ToLegacySkillLevel(
                request.MinimumRating.GetValueOrDefault(),
                request.MaximumRating.GetValueOrDefault()),
            GenderPreference = request.GenderPolicy == "SPECIFIC"
                && (request.RequiredGender is "MALE" or "FEMALE")
                    ? request.RequiredGender
                    : GenderPreferences.Both,
            RequiredPlayerCount = request.RequiredPlayerCount,
            ParticipationFee = request.ParticipationFee,
            GameType = request.GameType,
            MinimumRating = request.MinimumRating,
            MaximumRating = request.MaximumRating,
            GenderPolicy = request.GenderPolicy,
            RequiredGender = request.RequiredGender
        };

    private static string ToLegacySkillLevel(int minimumRating, int maximumRating) =>
        SkillLevels.FromRating((minimumRating + maximumRating) / 2);

    private static void AppendBaseQuery(StringBuilder sql, DynamicParameters parameters)
    {
        sql.AppendLine("""
            WITH match_data AS (
                SELECT
                    pm.id AS Id,
                    pm.title AS Title,
                    pm.branch_id AS BranchId,
                    pm.booking_id AS BookingId,
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
                  AND ((pm.match_date + pm.start_time)
                        AT TIME ZONE COALESCE(NULLIF(b.time_zone, ''), 'Asia/Ho_Chi_Minh')) > NOW()
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

        if (!string.IsNullOrWhiteSpace(request.GenderPreference))
        {
            sql.AppendLine("""
                AND (UPPER(GenderPreference) = @GenderPreference
                     OR (UPPER(GenderPreference) = 'BOTH' AND @GenderPreference IN ('MALE', 'FEMALE')))
                """);
            parameters.Add("GenderPreference", request.GenderPreference.Trim().ToUpperInvariant());
        }

        if (request.BranchId is Guid branchId)
        {
            sql.AppendLine("AND BranchId = @BranchId");
            parameters.Add("BranchId", branchId);
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
                sql.AppendLine("ORDER BY CreatedAt ASC, Id ASC");
                break;
            case "match_date_asc":
                sql.AppendLine("ORDER BY MatchDate ASC, StartTime ASC, Id ASC");
                break;
            case "match_date_desc":
                sql.AppendLine("ORDER BY MatchDate DESC, StartTime DESC, Id DESC");
                break;
            default:
                sql.AppendLine("ORDER BY CreatedAt DESC, Id DESC");
                break;
        }
    }

    private sealed class PlayerMatchDetailRow
    {
        public Guid Id { get; init; }
        public string Title { get; init; } = string.Empty;
        public string? Description { get; init; }
        public Guid? BookingId { get; init; }
        public string RoomCode { get; init; } = string.Empty;
        public Guid BranchId { get; init; }
        public string BranchName { get; init; } = string.Empty;
        public string City { get; init; } = string.Empty;
        public string District { get; init; } = string.Empty;
        public string? CourtName { get; init; }
        public DateOnly? MatchDate { get; init; }
        public TimeOnly? StartTime { get; init; }
        public TimeOnly? EndTime { get; init; }
        public string SkillLevel { get; init; } = string.Empty;
        public string GenderPreference { get; init; } = GenderPreferences.Both;
        public string? GameType { get; init; }
        public int? MinimumRating { get; init; }
        public int? MaximumRating { get; init; }
        public string? GenderPolicy { get; init; }
        public string? RequiredGender { get; init; }
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
