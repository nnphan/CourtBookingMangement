using System.Data;
using CourtBookingManagement.Application.Abstractions.Data;
using CourtBookingManagement.Application.Matching.Models;
using CourtBookingManagement.Application.Matching.Models.Requests;
using CourtBookingManagement.Application.Matching.Models.Responses;
using CourtBookingManagement.Application.Matching.Repositories;
using CourtBookingManagement.Domain.Abstractions;
using FluentValidation;

namespace CourtBookingManagement.Application.Matching.Services;

public sealed class PlayerMatchService(
    IPlayerMatchRepository repository,
    IValidator<CreatePlayerMatchRequest> validator,
    ISqlConnectionFactory sqlConnectionFactory) : IPlayerMatchService
{
    private static readonly Error InvalidMatchId = new("PLAYER_MATCH.INVALID_ID", "A valid player match id is required.");

    private static readonly Error MatchNotFound = new("PLAYER_MATCH.NOT_FOUND", "The player match was not found.");

    public async Task<Result<PagedResult<PlayerMatchResponse>>> GetMatchesAsync(
        PlayerMatchSearchRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return Result.Failure<PagedResult<PlayerMatchResponse>>(new Error("INVALID_REQUEST", "Request cannot be null."));
        }

        var normalizedRequest = new PlayerMatchSearchRequest
        {
            Keyword = NormalizeText(request.Keyword),
            MatchDate = request.MatchDate,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            SkillLevel = NormalizeText(request.SkillLevel),
            City = NormalizeText(request.City),
            District = NormalizeText(request.District),
            SortBy = NormalizeSort(request.SortBy),
            Page = request.Page <= 0 ? 1 : request.Page,
            PageSize = request.PageSize <= 0 ? 9 : Math.Min(request.PageSize, 50)
        };

        try
        {
            var matches = await repository.SearchAsync(normalizedRequest, cancellationToken);
            return Result.Success(matches);
        }
        catch (Exception exception)
        {
            return Result.Failure<PagedResult<PlayerMatchResponse>>(Error.FromException(exception));
        }
    }

    public async Task<Result<PlayerMatchDetailResponse>> GetMatchByIdAsync(
        Guid matchId,
        CancellationToken cancellationToken)
    {
        if (matchId == Guid.Empty)
        {
            return Result.Failure<PlayerMatchDetailResponse>(InvalidMatchId);
        }

        try
        {
            var match = await repository.GetByIdAsync(matchId, cancellationToken);
            if (match is null)
            {
                return Result.Failure<PlayerMatchDetailResponse>(MatchNotFound);
            }

            match.RemainingSlots = Math.Max(match.MaxPlayers - match.CurrentPlayers, 0);
            match.CanJoin = string.Equals(match.Status, "OPEN", StringComparison.OrdinalIgnoreCase)
                && match.CurrentPlayers < match.MaxPlayers;
            match.IsFull = match.CurrentPlayers >= match.MaxPlayers;
            match.ParticipantCount = match.Participants.Count;

            return Result.Success(match);
        }
        catch (Exception exception)
        {
            return Result.Failure<PlayerMatchDetailResponse>(Error.FromException(exception));
        }
    }

    public async Task<Result<CreatePlayerMatchResponse>> CreateAsync(
        CreatePlayerMatchRequest request,
        Guid createdBy,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return Result.Failure<CreatePlayerMatchResponse>(new Error("PLAYER_MATCH.INVALID_REQUEST", "Request cannot be null."));
        }

        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<CreatePlayerMatchResponse>(
                new Error("PLAYER_MATCH.VALIDATION", string.Join("; ", validation.Errors.Select(error => error.ErrorMessage))));
        }

        if (createdBy == Guid.Empty)
        {
            return Result.Failure<CreatePlayerMatchResponse>(new Error("PLAYER_MATCH.INVALID_USER", "The user creating the match is invalid."));
        }

        if (!await repository.BranchExistsAsync(request.BranchId, cancellationToken))
        {
            return Result.Failure<CreatePlayerMatchResponse>(new Error("PLAYER_MATCH.BRANCH_NOT_FOUND", "Branch not found."));
        }

        if (request.CourtId is Guid courtId && !await repository.CourtExistsAsync(courtId, request.BranchId, cancellationToken))
        {
            return Result.Failure<CreatePlayerMatchResponse>(new Error("PLAYER_MATCH.COURT_NOT_FOUND", "Court not found for the selected branch."));
        }

        if (request.CourtId.HasValue && await repository.HasOverlappingMatchAsync(request.CourtId.Value, request.MatchDate, request.StartTime, request.EndTime, cancellationToken))
        {
            return Result.Failure<CreatePlayerMatchResponse>(new Error("PLAYER_MATCH.COURT_CONFLICT", "The selected court is already booked for that time."));
        }

        if (await repository.DuplicateMatchExistsAsync(createdBy, request.CourtId, request.MatchDate, request.StartTime, request.EndTime, cancellationToken))
        {
            return Result.Failure<CreatePlayerMatchResponse>(new Error("PLAYER_MATCH.DUPLICATE", "You already have a match scheduled for that time."));
        }

        using var connection = sqlConnectionFactory.CreateConnection();
        using var transaction = connection.BeginTransaction();

        try
        {
            var matchId = await repository.CreateAsync(request, createdBy, transaction, cancellationToken);
            await repository.CreateParticipantAsync(matchId, createdBy, transaction, cancellationToken);
            transaction.Commit();

            return Result.Success(new CreatePlayerMatchResponse
            {
                Id = matchId,
                Message = "Player match created successfully."
            });
        }
        catch (Exception exception)
        {
            transaction.Rollback();
            return Result.Failure<CreatePlayerMatchResponse>(Error.FromException(exception));
        }
    }

    public async Task<Result<JoinMatchResponse>> JoinMatchAsync(
        Guid matchId,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        if (matchId == Guid.Empty)
        {
            return Result.Failure<JoinMatchResponse>(new Error("PLAYER_MATCH.INVALID_ID", "A valid player match id is required."));
        }

        if (currentUserId == Guid.Empty)
        {
            return Result.Failure<JoinMatchResponse>(new Error("PLAYER_MATCH.INVALID_USER", "The current user is invalid."));
        }

        var match = await repository.GetMatchByIdAsync(matchId, cancellationToken);
        if (match is null)
        {
            return Result.Failure<JoinMatchResponse>(new Error("PLAYER_MATCH.NOT_FOUND", "Player match not found."));
        }

        if (!string.Equals(match.Status, "OPEN", StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure<JoinMatchResponse>(new Error("PLAYER_MATCH.NOT_OPEN", "Match is not open for joining."));
        }

        if (match.MatchDate < DateOnly.FromDateTime(DateTime.UtcNow))
        {
            return Result.Failure<JoinMatchResponse>(new Error("PLAYER_MATCH.MATCH_EXPIRED", "Expired matches cannot be joined."));
        }

        if (match.CurrentPlayers >= match.MaxPlayers)
        {
            return Result.Failure<JoinMatchResponse>(new Error("PLAYER_MATCH.MATCH_FULL", "Match is already full."));
        }

        if (match.CreatedBy == currentUserId)
        {
            return Result.Failure<JoinMatchResponse>(new Error("PLAYER_MATCH.HOST_ALREADY_PARTICIPANT", "Host is already a participant."));
        }

        if (await repository.IsParticipantAsync(matchId, currentUserId, cancellationToken))
        {
            return Result.Failure<JoinMatchResponse>(new Error("PLAYER_MATCH.ALREADY_JOINED", "You have already joined this match."));
        }

        if (await repository.HasPendingJoinRequestAsync(matchId, currentUserId, cancellationToken))
        {
            return Result.Failure<JoinMatchResponse>(new Error("PLAYER_MATCH.REQUEST_EXISTS", "Join request already exists."));
        }

        using var connection = sqlConnectionFactory.CreateConnection();
        using var transaction = connection.BeginTransaction();

        try
        {
            var joinRequestId = await repository.CreateJoinRequestAsync(matchId, currentUserId, transaction, cancellationToken);
            await repository.CreateNotificationAsync(
                matchId,
                match.CreatedBy,
                "A player requested to join your match.",
                transaction,
                cancellationToken);

            transaction.Commit();

            return Result.Success(new JoinMatchResponse
            {
                MatchId = matchId,
                JoinRequestId = joinRequestId,
                Status = "PENDING",
                Message = "Join request submitted successfully."
            });
        }
        catch (Exception exception)
        {
            transaction.Rollback();
            return Result.Failure<JoinMatchResponse>(Error.FromException(exception));
        }
    }

    public async Task<Result<ApproveJoinRequestResponse>> ApproveJoinRequestAsync(
        Guid matchId,
        Guid requestId,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        if (matchId == Guid.Empty)
        {
            return Result.Failure<ApproveJoinRequestResponse>(new Error("PLAYER_MATCH.INVALID_ID", "A valid player match id is required."));
        }

        if (requestId == Guid.Empty)
        {
            return Result.Failure<ApproveJoinRequestResponse>(new Error("PLAYER_MATCH.INVALID_REQUEST_ID", "A valid join request id is required."));
        }

        if (currentUserId == Guid.Empty)
        {
            return Result.Failure<ApproveJoinRequestResponse>(new Error("PLAYER_MATCH.INVALID_USER", "The current user is invalid."));
        }

        var match = await repository.GetMatchByIdAsync(matchId, cancellationToken);
        if (match is null)
        {
            return Result.Failure<ApproveJoinRequestResponse>(new Error("PLAYER_MATCH.NOT_FOUND", "Player match not found."));
        }

        if (!string.Equals(match.Status, "OPEN", StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure<ApproveJoinRequestResponse>(new Error("PLAYER_MATCH.NOT_OPEN", "Match is not open for new approvals."));
        }

        if (match.CreatedBy != currentUserId)
        {
            return Result.Failure<ApproveJoinRequestResponse>(new Error("PLAYER_MATCH.NOT_HOST", "Only the host can approve join requests."));
        }

        var joinRequest = await repository.GetJoinRequestAsync(requestId, cancellationToken);
        if (joinRequest is null)
        {
            return Result.Failure<ApproveJoinRequestResponse>(new Error("PLAYER_MATCH.REQUEST_NOT_FOUND", "Join request was not found."));
        }

        if (joinRequest.MatchId != matchId)
        {
            return Result.Failure<ApproveJoinRequestResponse>(new Error("PLAYER_MATCH.REQUEST_MISMATCH", "Join request does not belong to the selected match."));
        }

        if (!string.Equals(joinRequest.Status, "PENDING", StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure<ApproveJoinRequestResponse>(new Error("PLAYER_MATCH.REQUEST_NOT_PENDING", "Only pending join requests can be approved."));
        }

        if (match.CurrentPlayers >= match.MaxPlayers)
        {
            return Result.Failure<ApproveJoinRequestResponse>(new Error("PLAYER_MATCH.MATCH_FULL", "Match is already full."));
        }

        if (await repository.IsParticipantAsync(matchId, joinRequest.UserId, cancellationToken))
        {
            return Result.Failure<ApproveJoinRequestResponse>(new Error("PLAYER_MATCH.ALREADY_JOINED", "Player is already a participant in this match."));
        }

        using var connection = sqlConnectionFactory.CreateConnection();
        using var transaction = connection.BeginTransaction();

        try
        {
            await repository.ApproveJoinRequestAsync(requestId, currentUserId, transaction, cancellationToken);
            await repository.CreateParticipantAsync(matchId, joinRequest.UserId, "PLAYER", transaction, cancellationToken);
            await repository.IncrementCurrentPlayersAsync(matchId, transaction, cancellationToken);

            var updatedPlayerCount = match.CurrentPlayers + 1;
            if (updatedPlayerCount >= match.MaxPlayers)
            {
                await repository.UpdateMatchStatusAsync(matchId, "FULL", transaction, cancellationToken);
                await repository.CreateMatchFullNotificationsAsync(matchId, transaction, cancellationToken);
            }

            await repository.CreateNotificationAsync(
                joinRequest.UserId,
                matchId,
                "JOIN_APPROVED",
                "Join request approved",
                "Your join request was approved by the match host.",
                transaction,
                cancellationToken);

            transaction.Commit();

            return Result.Success(new ApproveJoinRequestResponse
            {
                MatchId = matchId,
                RequestId = requestId,
                UserId = joinRequest.UserId,
                Status = "APPROVED",
                Message = "Join request approved successfully."
            });
        }
        catch (Exception exception)
        {
            transaction.Rollback();
            return Result.Failure<ApproveJoinRequestResponse>(Error.FromException(exception));
        }
    }

    public async Task<Result<RejectJoinRequestResponse>> RejectJoinRequestAsync(
        Guid matchId,
        Guid requestId,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        if (matchId == Guid.Empty)
        {
            return Result.Failure<RejectJoinRequestResponse>(new Error("PLAYER_MATCH.INVALID_ID", "A valid player match id is required."));
        }

        if (requestId == Guid.Empty)
        {
            return Result.Failure<RejectJoinRequestResponse>(new Error("PLAYER_MATCH.INVALID_REQUEST_ID", "A valid join request id is required."));
        }

        if (currentUserId == Guid.Empty)
        {
            return Result.Failure<RejectJoinRequestResponse>(new Error("PLAYER_MATCH.INVALID_USER", "The current user is invalid."));
        }

        var match = await repository.GetMatchByIdAsync(matchId, cancellationToken);
        if (match is null)
        {
            return Result.Failure<RejectJoinRequestResponse>(new Error("PLAYER_MATCH.NOT_FOUND", "Player match not found."));
        }

        var joinRequest = await repository.GetJoinRequestAsync(requestId, cancellationToken);
        if (joinRequest is null)
        {
            return Result.Failure<RejectJoinRequestResponse>(new Error("PLAYER_MATCH.REQUEST_NOT_FOUND", "Join request not found."));
        }

        if (joinRequest.MatchId != matchId)
        {
            return Result.Failure<RejectJoinRequestResponse>(new Error("PLAYER_MATCH.REQUEST_MISMATCH", "Join request does not belong to this match."));
        }

        if (match.CreatedBy != currentUserId)
        {
            return Result.Failure<RejectJoinRequestResponse>(new Error("PLAYER_MATCH.NOT_HOST", "You are not allowed to reject join requests."));
        }

        if (!string.Equals(match.Status, "OPEN", StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure<RejectJoinRequestResponse>(new Error("PLAYER_MATCH.NOT_OPEN", "Join requests can only be rejected while the match is open."));
        }

        if (!string.Equals(joinRequest.Status, "PENDING", StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure<RejectJoinRequestResponse>(new Error("PLAYER_MATCH.REQUEST_NOT_PENDING", "Only pending requests can be rejected."));
        }

        using var connection = sqlConnectionFactory.CreateConnection();
        using var transaction = connection.BeginTransaction();

        try
        {
            var rejected = await repository.RejectJoinRequestAsync(requestId, transaction, cancellationToken);
            if (!rejected)
            {
                transaction.Rollback();
                return Result.Failure<RejectJoinRequestResponse>(new Error(
                    "PLAYER_MATCH.REQUEST_STATE_CHANGED",
                    "The match or join request changed. Refresh and try again."));
            }

            await repository.CreateNotificationAsync(
                joinRequest.UserId,
                matchId,
                "JOIN_REJECTED",
                "Join request rejected",
                "Your request to join the match has been rejected.",
                transaction,
                cancellationToken);

            transaction.Commit();

            return Result.Success(new RejectJoinRequestResponse
            {
                MatchId = matchId,
                RequestId = requestId,
                UserId = joinRequest.UserId,
                Status = "REJECTED",
                Message = "Join request rejected successfully."
            });
        }
        catch (Exception exception)
        {
            transaction.Rollback();
            return Result.Failure<RejectJoinRequestResponse>(Error.FromException(exception));
        }
    }

    private static string? NormalizeText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string NormalizeSort(string? value)
    {
        var normalized = value?.Trim();

        if (string.IsNullOrWhiteSpace(normalized))
        {
            return "latest";
        }

        return normalized.ToLowerInvariant() switch
        {
            "oldest" => "oldest",
            "match_date_asc" => "match_date_asc",
            "match_date_desc" => "match_date_desc",
            _ => "latest"
        };
    }
}
