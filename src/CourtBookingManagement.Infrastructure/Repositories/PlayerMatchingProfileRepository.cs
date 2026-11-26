using System.Data;
using CourtBookingManagement.Application.Abstractions.Data;
using CourtBookingManagement.Application.Matching.Models;
using CourtBookingManagement.Application.Matching.Models.Requests;
using CourtBookingManagement.Application.Matching.Models.Responses;
using CourtBookingManagement.Application.Matching.Repositories;
using Dapper;

namespace CourtBookingManagement.Infrastructure.Repositories;

public sealed class PlayerMatchingProfileRepository(ISqlConnectionFactory sqlConnectionFactory) : IPlayerMatchingProfileRepository
{
    public async Task<PlayerMatchingProfileResponse?> GetProfileAsync(Guid userId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                current_rating AS CurrentRating,
                rating_confidence AS RatingConfidence,
                rating_source AS RatingSource,
                matches_played AS MatchesPlayed,
                gender AS Gender,
                is_profile_public AS IsProfilePublic,
                is_matching_enabled AS IsMatchingEnabled
            FROM matching.player_profiles
            WHERE user_id = @UserId;
            """;

        using var connection = sqlConnectionFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<ProfileRow>(
            new CommandDefinition(sql, new { UserId = userId }, cancellationToken: cancellationToken));

        return row is null ? null : ToResponse(row);
    }

    public async Task<PlayerMatchingProfileResponse> UpdateProfileAsync(
        Guid userId,
        UpdatePlayerMatchingProfileRequest request,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO matching.player_profiles
                (user_id, current_rating, gender, is_profile_public, is_matching_enabled, created_by, updated_by)
            VALUES
                (@UserId, @CurrentRating, @Gender, @IsProfilePublic, @IsMatchingEnabled, @UserId, @UserId)
            ON CONFLICT (user_id) DO UPDATE SET
                current_rating = EXCLUDED.current_rating,
                rating_source = 'SELF_ASSESSMENT',
                gender = EXCLUDED.gender,
                is_profile_public = EXCLUDED.is_profile_public,
                is_matching_enabled = EXCLUDED.is_matching_enabled,
                updated_at = now(),
                updated_by = EXCLUDED.updated_by
            RETURNING
                current_rating AS CurrentRating,
                rating_confidence AS RatingConfidence,
                rating_source AS RatingSource,
                matches_played AS MatchesPlayed,
                gender AS Gender,
                is_profile_public AS IsProfilePublic,
                is_matching_enabled AS IsMatchingEnabled;
            """;

        using var connection = sqlConnectionFactory.CreateConnection();
        var row = await connection.QuerySingleAsync<ProfileRow>(
            new CommandDefinition(sql, new
            {
                UserId = userId,
                request.CurrentRating,
                request.Gender,
                request.IsProfilePublic,
                request.IsMatchingEnabled
            }, cancellationToken: cancellationToken));

        return ToResponse(row);
    }

    public async Task<PlayerMatchPreferencesResponse?> GetPreferencesAsync(Guid userId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                COALESCE(preferences.skill_weight, 40) AS SkillWeight,
                COALESCE(preferences.time_weight, 30) AS TimeWeight,
                COALESCE(preferences.branch_weight, 20) AS BranchWeight,
                COALESCE(preferences.gender_weight, 10) AS GenderWeight,
                preferences.preferred_start_time AS PreferredStartTime,
                preferences.preferred_end_time AS PreferredEndTime,
                COALESCE(preferences.minimum_time_overlap_minutes, 60) AS MinimumTimeOverlapMinutes,
                preferences.maximum_participation_fee AS MaximumParticipationFee
            FROM matching.player_profiles profile
            LEFT JOIN matching.player_match_preferences preferences
                ON preferences.player_profile_id = profile.id
            WHERE profile.user_id = @UserId;

            SELECT preferred_branch.branch_id
            FROM matching.player_profiles profile
            INNER JOIN matching.player_preferred_branches preferred_branch
                ON preferred_branch.player_profile_id = profile.id
            WHERE profile.user_id = @UserId
            ORDER BY preferred_branch.priority_order;
            """;

        using var connection = sqlConnectionFactory.CreateConnection();
        using var results = await connection.QueryMultipleAsync(
            new CommandDefinition(sql, new { UserId = userId }, cancellationToken: cancellationToken));

        var row = await results.ReadSingleOrDefaultAsync<PreferencesRow>();
        if (row is null)
        {
            return null;
        }

        return new PlayerMatchPreferencesResponse
        {
            Preferences = new PlayerMatchPreferenceWeights
            {
                SkillWeight = row.SkillWeight,
                TimeWeight = row.TimeWeight,
                BranchWeight = row.BranchWeight,
                GenderWeight = row.GenderWeight
            },
            BranchIds = (await results.ReadAsync<Guid>()).AsList(),
            PreferredStartTime = row.PreferredStartTime,
            PreferredEndTime = row.PreferredEndTime,
            MinimumTimeOverlapMinutes = row.MinimumTimeOverlapMinutes,
            MaximumParticipationFee = row.MaximumParticipationFee
        };
    }

    public async Task<bool> BranchesExistAsync(IReadOnlyList<Guid> branchIds, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COUNT(*)
            FROM core.branches
            WHERE id = ANY(@BranchIds);
            """;

        using var connection = sqlConnectionFactory.CreateConnection();
        var count = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, new { BranchIds = branchIds.ToArray() }, cancellationToken: cancellationToken));

        return count == branchIds.Count;
    }

    public async Task<bool> UpdatePreferencesAsync(
        Guid userId,
        UpdatePlayerMatchPreferencesRequest request,
        CancellationToken cancellationToken)
    {
        using var connection = sqlConnectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);

        const string profileSql = """
            SELECT id
            FROM matching.player_profiles
            WHERE user_id = @UserId
            FOR UPDATE;
            """;
        var profileId = await connection.QuerySingleOrDefaultAsync<Guid?>(
            new CommandDefinition(profileSql, new { UserId = userId }, transaction, cancellationToken: cancellationToken));
        if (profileId is null)
        {
            return false;
        }

        const string preferenceSql = """
            INSERT INTO matching.player_match_preferences
                (player_profile_id, skill_weight, time_weight, branch_weight, gender_weight,
                 preferred_start_time, preferred_end_time, minimum_time_overlap_minutes,
                 maximum_participation_fee, updated_at)
            VALUES
                (@ProfileId, @SkillWeight, @TimeWeight, @BranchWeight, @GenderWeight,
                 @PreferredStartTime, @PreferredEndTime, @MinimumTimeOverlapMinutes,
                 @MaximumParticipationFee, now())
            ON CONFLICT (player_profile_id) DO UPDATE SET
                skill_weight = EXCLUDED.skill_weight,
                time_weight = EXCLUDED.time_weight,
                branch_weight = EXCLUDED.branch_weight,
                gender_weight = EXCLUDED.gender_weight,
                preferred_start_time = EXCLUDED.preferred_start_time,
                preferred_end_time = EXCLUDED.preferred_end_time,
                minimum_time_overlap_minutes = EXCLUDED.minimum_time_overlap_minutes,
                maximum_participation_fee = EXCLUDED.maximum_participation_fee,
                updated_at = now();
            """;
        await connection.ExecuteAsync(new CommandDefinition(preferenceSql, new
        {
            ProfileId = profileId.Value,
            SkillWeight = request.Preferences.SkillWeight,
            TimeWeight = request.Preferences.TimeWeight,
            BranchWeight = request.Preferences.BranchWeight,
            GenderWeight = request.Preferences.GenderWeight,
            request.PreferredStartTime,
            request.PreferredEndTime,
            request.MinimumTimeOverlapMinutes,
            request.MaximumParticipationFee
        }, transaction, cancellationToken: cancellationToken));

        const string deleteBranchesSql = """
            DELETE FROM matching.player_preferred_branches
            WHERE player_profile_id = @ProfileId;
            """;
        await connection.ExecuteAsync(new CommandDefinition(
            deleteBranchesSql, new { ProfileId = profileId.Value }, transaction, cancellationToken: cancellationToken));

        const string insertBranchesSql = """
            INSERT INTO matching.player_preferred_branches (player_profile_id, branch_id, priority_order)
            VALUES (@ProfileId, @BranchId, @PriorityOrder);
            """;
        var branches = request.BranchIds
            .Select((branchId, priorityOrder) => new
            {
                ProfileId = profileId.Value,
                BranchId = branchId,
                PriorityOrder = priorityOrder
            });
        await connection.ExecuteAsync(new CommandDefinition(
            insertBranchesSql, branches, transaction, cancellationToken: cancellationToken));

        transaction.Commit();
        return true;
    }

    private static PlayerMatchingProfileResponse ToResponse(ProfileRow row) => new()
    {
        CurrentRating = row.CurrentRating,
        RatingBand = row.CurrentRating switch
        {
            <= 199 => "BEGINNER",
            <= 399 => "BASIC",
            <= 599 => "INTERMEDIATE",
            <= 749 => "GOOD",
            <= 899 => "ADVANCED",
            _ => "COMPETITIVE"
        },
        RatingConfidence = row.RatingConfidence,
        RatingSource = row.RatingSource,
        MatchesPlayed = row.MatchesPlayed,
        Gender = row.Gender,
        IsProfilePublic = row.IsProfilePublic,
        IsMatchingEnabled = row.IsMatchingEnabled
    };

    private sealed class ProfileRow
    {
        public int CurrentRating { get; init; }
        public decimal RatingConfidence { get; init; }
        public string RatingSource { get; init; } = string.Empty;
        public int MatchesPlayed { get; init; }
        public string? Gender { get; init; }
        public bool IsProfilePublic { get; init; }
        public bool IsMatchingEnabled { get; init; }
    }

    private sealed class PreferencesRow
    {
        public decimal SkillWeight { get; init; }
        public decimal TimeWeight { get; init; }
        public decimal BranchWeight { get; init; }
        public decimal GenderWeight { get; init; }
        public TimeOnly? PreferredStartTime { get; init; }
        public TimeOnly? PreferredEndTime { get; init; }
        public int MinimumTimeOverlapMinutes { get; init; }
        public decimal? MaximumParticipationFee { get; init; }
    }
}
