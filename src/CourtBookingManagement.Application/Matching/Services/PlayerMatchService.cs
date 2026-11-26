using CourtBookingManagement.Application.Abstractions.Data;
using CourtBookingManagement.Application.Bookings.Interfaces;
using CourtBookingManagement.Application.Common.Constants;
using CourtBookingManagement.Application.Common.Enums;
using CourtBookingManagement.Application.Matching.Models;
using CourtBookingManagement.Application.Matching.Models.Requests;
using CourtBookingManagement.Application.Matching.Models.Responses;
using CourtBookingManagement.Application.Matching.Repositories;
using CourtBookingManagement.Domain.Abstractions;
using FluentValidation;
using Microsoft.Extensions.Logging;
using System.Data.Common;
using System.Data;

namespace CourtBookingManagement.Application.Matching.Services;

public sealed class PlayerMatchService(
    IPlayerMatchRepository repository,
    IBookingRepository bookingRepository,
    IValidator<CreatePlayerMatchRequest> validator,
    IValidator<PlayerMatchDraftRequest> draftValidator,
    IValidator<PlayerMatchSearchRequest> searchRequestValidator,
    IValidator<PlayerMatchScoringCriteriaRequest> scoringCriteriaValidator,
    IPlayerMatchingProfileService profileService,
    ISqlConnectionFactory sqlConnectionFactory,
    ILogger<PlayerMatchService> logger) : IPlayerMatchService
{
    private static readonly Error InvalidMatchId = new("PLAYER_MATCH.INVALID_ID", "A valid player match id is required.");

    private static readonly Error InvalidUser = new("PLAYER_MATCH.INVALID_USER", "The current user is invalid.");

    private static readonly Error MatchNotFound = new("PLAYER_MATCH.NOT_FOUND", "The player match was not found.");

    public async Task<Result<PagedResult<PlayerMatchResponse>>> GetMatchesAsync(
        PlayerMatchSearchRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return Result.Failure<PagedResult<PlayerMatchResponse>>(new Error("INVALID_REQUEST", "Request cannot be null."));
        }

        var validation = await searchRequestValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<PagedResult<PlayerMatchResponse>>(
                new Error("PLAYER_MATCH.VALIDATION", string.Join("; ", validation.Errors.Select(error => error.ErrorMessage))));
        }

        var normalizedRequest = new PlayerMatchSearchRequest
        {
            Keyword = NormalizeText(request.Keyword),
            MatchDate = request.MatchDate,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            SkillLevel = NormalizeText(request.SkillLevel),
            GenderPreference = NormalizeText(request.GenderPreference),
            BranchId = request.BranchId,
            City = NormalizeText(request.City),
            District = NormalizeText(request.District),
            SortBy = NormalizeSort(request.SortBy),
            Page = request.Page <= 0 ? 1 : request.Page,
            PageSize = request.PageSize <= 0 ? 9 : Math.Min(request.PageSize, 50)
        };

        var matches = await repository.SearchAsync(normalizedRequest, cancellationToken);
        return Result.Success(matches);
    }

    public async Task<Result<PersonalizedPlayerMatchSearchResult>> SearchPersonalizedAsync(
        PersonalizedPlayerMatchSearchRequest request,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return Result.Failure<PersonalizedPlayerMatchSearchResult>(
                new Error("SEARCH_CRITERIA_INVALID", "Search criteria cannot be null."));
        }

        if (currentUserId == Guid.Empty)
        {
            return Result.Failure<PersonalizedPlayerMatchSearchResult>(InvalidUser);
        }

        if (request.Page < 1 || request.PageSize is < 1 or > 50)
        {
            return Result.Failure<PersonalizedPlayerMatchSearchResult>(
                new Error("SEARCH_CRITERIA_INVALID", "Page must be positive and page size must be between 1 and 50."));
        }

        var criteriaResult = await ResolveScoringCriteriaAsync(request, currentUserId, cancellationToken);
        if (criteriaResult.IsFailure)
        {
            return Result.Failure<PersonalizedPlayerMatchSearchResult>(criteriaResult.Error);
        }

        var resolved = criteriaResult.Value;
        var candidates = await repository.SearchPersonalizedCandidatesAsync(
            currentUserId,
            resolved.Profile?.CurrentRating,
            resolved.Profile?.Gender,
            resolved.Criteria,
            null,
            candidateLimit: 301,
            cancellationToken);

        var candidateLimitReached = candidates.Count > 300;
        var rankedCandidates = candidates
            .Take(300)
            .Select(candidate => new
            {
                Candidate = candidate,
                Scored = ScoreCandidate(candidate, resolved.Profile, resolved.Weights, resolved.Criteria)
            })
            .OrderByDescending(item => item.Scored.RawMatchScore)
            .ThenByDescending(item => item.Scored.Response.CourtReservationStatus == "COURT_RESERVED")
            .ThenBy(item => item.Scored.Response.AvailableSlots)
            .ThenBy(item => item.Scored.Response.MatchDate)
            .ThenBy(item => item.Scored.Response.StartTime)
            .ThenByDescending(item => item.Candidate.CreatedAt)
            .ThenBy(item => item.Scored.Response.RoomId)
            .ToList();

        var offset = ((long)request.Page - 1) * request.PageSize;
        List<PersonalizedPlayerMatchResponse> pageItems = offset >= rankedCandidates.Count
            ? []
            : rankedCandidates.Skip((int)offset).Take(request.PageSize).Select(item => item.Scored.Response).ToList();
        var pagedResults = PagedResult<PersonalizedPlayerMatchResponse>.Create(
            pageItems,
            request.Page,
            request.PageSize,
            rankedCandidates.Count);

        if (request.SaveAsDefault)
        {
            var saveResult = await profileService.UpdatePreferencesAsync(
                new UpdatePlayerMatchPreferencesRequest
                {
                    Preferences = CopyWeights(resolved.Weights),
                    BranchIds = resolved.Criteria.BranchIds,
                    PreferredStartTime = resolved.Criteria.PreferredStartTime
                        ?? (resolved.Criteria.EndTime.HasValue ? resolved.Criteria.StartTime : null),
                    PreferredEndTime = resolved.Criteria.PreferredEndTime
                        ?? (resolved.Criteria.StartTime.HasValue ? resolved.Criteria.EndTime : null),
                    MinimumTimeOverlapMinutes = resolved.Criteria.PreferredStartTime.HasValue
                        ? resolved.Criteria.MinimumTimeOverlapMinutes : 1,
                    MaximumParticipationFee = resolved.Criteria.MaximumParticipationFee
                },
                currentUserId,
                cancellationToken);

            if (saveResult.IsFailure)
            {
                return Result.Failure<PersonalizedPlayerMatchSearchResult>(saveResult.Error);
            }
        }

        return Result.Success(new PersonalizedPlayerMatchSearchResult
        {
            Data = new PersonalizedPlayerMatchSearchResponse
            {
                Items = pagedResults.Items,
                AppliedWeights = CopyWeights(resolved.Weights),
                CandidateLimitReached = candidateLimitReached
            },
            Metadata = pagedResults.Metadata
        });
    }

    public async Task<Result<PersonalizedPlayerMatchResponse>> ScoreMatchAsync(
        Guid matchId,
        PlayerMatchScoringCriteriaRequest request,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        if (matchId == Guid.Empty)
        {
            return Result.Failure<PersonalizedPlayerMatchResponse>(InvalidMatchId);
        }

        if (currentUserId == Guid.Empty)
        {
            return Result.Failure<PersonalizedPlayerMatchResponse>(InvalidUser);
        }

        var criteriaResult = await ResolveScoringCriteriaAsync(request, currentUserId, cancellationToken);
        if (criteriaResult.IsFailure)
        {
            return Result.Failure<PersonalizedPlayerMatchResponse>(criteriaResult.Error);
        }

        var resolved = criteriaResult.Value;
        var candidates = await repository.SearchPersonalizedCandidatesAsync(
            currentUserId,
            resolved.Profile?.CurrentRating,
            resolved.Profile?.Gender,
            resolved.Criteria,
            matchId,
            candidateLimit: 1,
            cancellationToken);

        if (candidates.Count == 0)
        {
            return Result.Failure<PersonalizedPlayerMatchResponse>(
                new Error("PLAYER_NOT_ELIGIBLE", "The player is not eligible for this room."));
        }

        return Result.Success(ScoreCandidate(candidates[0], resolved.Profile, resolved.Weights, resolved.Criteria).Response);
    }

    public async Task<Result<PagedResult<HostedPlayerMatchResponse>>> GetHostedMatchesAsync(
        GetHostedMatchesRequest request,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        if (currentUserId == Guid.Empty)
        {
            return Result.Failure<PagedResult<HostedPlayerMatchResponse>>(InvalidUser);
        }

        var status = NormalizeStatus(request.Status);
        if (status is not null && !PlayerMatchStatus.All.Contains(status))
        {
            return Result.Failure<PagedResult<HostedPlayerMatchResponse>>(
                new Error("PLAYER_MATCH.INVALID_STATUS", "Status is not a supported player-match status."));
        }

        var normalizedRequest = new GetHostedMatchesRequest
        {
            Keyword = NormalizeText(request.Keyword),
            MatchDate = request.MatchDate,
            Status = status,
            Page = request.Page <= 0 ? 1 : request.Page,
            PageSize = request.PageSize <= 0 ? 10 : Math.Min(request.PageSize, 50)
        };

        var matches = await repository.GetHostedMatchesAsync(currentUserId, normalizedRequest, cancellationToken);
        return Result.Success(matches);
    }

    public async Task<Result<IReadOnlyList<PlayerMatchJoinRequestResponse>>> GetJoinRequestsAsync(
        Guid matchId,
        GetPlayerMatchJoinRequestsRequest request,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        if (matchId == Guid.Empty)
        {
            return Result.Failure<IReadOnlyList<PlayerMatchJoinRequestResponse>>(InvalidMatchId);
        }

        if (currentUserId == Guid.Empty)
        {
            return Result.Failure<IReadOnlyList<PlayerMatchJoinRequestResponse>>(InvalidUser);
        }

        if (request is null)
        {
            return Result.Failure<IReadOnlyList<PlayerMatchJoinRequestResponse>>(
                new Error("INVALID_REQUEST", "Request cannot be null."));
        }

        var status = NormalizeStatus(request.Status);
        if (status is not null && !JoinRequestStatus.All.Contains(status))
        {
            return Result.Failure<IReadOnlyList<PlayerMatchJoinRequestResponse>>(
                new Error("PLAYER_MATCH.INVALID_REQUEST_STATUS", "Status must be PENDING, APPROVED, REJECTED, CANCELLED, or EXPIRED."));
        }

        var hostId = await repository.GetMatchHostIdAsync(matchId, cancellationToken);
        if (hostId is null)
        {
            return Result.Failure<IReadOnlyList<PlayerMatchJoinRequestResponse>>(MatchNotFound);
        }

        if (hostId.Value != currentUserId)
        {
            return Result.Failure<IReadOnlyList<PlayerMatchJoinRequestResponse>>(
                new Error("PLAYER_MATCH.NOT_HOST", "You are not allowed to manage join requests for this match."));
        }

        var joinRequests = await repository.GetJoinRequestsAsync(matchId, status, cancellationToken);
        return Result.Success(joinRequests);
    }

    public async Task<Result<PagedResult<MyMatchJoinRequestResponse>>> GetMyJoinRequestsAsync(
        GetMyMatchJoinRequestsRequest request,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        if (currentUserId == Guid.Empty)
        {
            return Result.Failure<PagedResult<MyMatchJoinRequestResponse>>(InvalidUser);
        }

        if (request is null)
        {
            return Result.Failure<PagedResult<MyMatchJoinRequestResponse>>(
                new Error("INVALID_REQUEST", "Request cannot be null."));
        }

        var status = NormalizeStatus(request.Status);
        if (status is not null && !JoinRequestStatus.All.Contains(status))
        {
            return Result.Failure<PagedResult<MyMatchJoinRequestResponse>>(
                new Error("PLAYER_MATCH.INVALID_REQUEST_STATUS", "Status must be PENDING, APPROVED, REJECTED, CANCELLED, or EXPIRED."));
        }

        var normalizedRequest = new GetMyMatchJoinRequestsRequest
        {
            Status = status,
            Page = request.Page <= 0 ? 1 : request.Page,
            PageSize = request.PageSize <= 0 ? 10 : Math.Min(request.PageSize, 50)
        };
        var joinRequests = await repository.GetMyJoinRequestsAsync(currentUserId, normalizedRequest, cancellationToken);
        return Result.Success(joinRequests);
    }

    public async Task<Result<PagedResult<ParticipatingPlayerMatchResponse>>> GetParticipatingMatchesAsync(
        GetParticipatingMatchesRequest request,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        if (currentUserId == Guid.Empty)
        {
            return Result.Failure<PagedResult<ParticipatingPlayerMatchResponse>>(InvalidUser);
        }

        var participationStatus = NormalizeStatus(request.ParticipationStatus);
        if (participationStatus is not null && !JoinRequestStatus.All.Contains(participationStatus))
        {
            return Result.Failure<PagedResult<ParticipatingPlayerMatchResponse>>(
                new Error("PLAYER_MATCH.INVALID_PARTICIPATION_STATUS", "ParticipationStatus must be PENDING, APPROVED, REJECTED, CANCELLED, or EXPIRED."));
        }

        var matchStatus = NormalizeStatus(request.MatchStatus);
        if (matchStatus is not null && !PlayerMatchStatus.All.Contains(matchStatus))
        {
            return Result.Failure<PagedResult<ParticipatingPlayerMatchResponse>>(
                new Error("PLAYER_MATCH.INVALID_STATUS", "MatchStatus is not a supported player-match status."));
        }

        var normalizedRequest = new GetParticipatingMatchesRequest
        {
            ParticipationStatus = participationStatus,
            MatchStatus = matchStatus,
            FromDate = request.FromDate,
            Page = request.Page <= 0 ? 1 : request.Page,
            PageSize = request.PageSize <= 0 ? 10 : Math.Min(request.PageSize, 50)
        };

        var matches = await repository.GetParticipatingMatchesAsync(currentUserId, normalizedRequest, cancellationToken);
        return Result.Success(matches);
    }

    private static string? NormalizeStatus(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();

    public async Task<Result<PlayerMatchDetailResponse>> GetMatchByIdAsync(
        Guid matchId,
        Guid? viewerUserId,
        CancellationToken cancellationToken)
    {
        if (matchId == Guid.Empty)
        {
            return Result.Failure<PlayerMatchDetailResponse>(InvalidMatchId);
        }

        var match = await repository.GetByIdAsync(matchId, viewerUserId, cancellationToken);
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

        var bookingRows = await bookingRepository.GetPlayerMatchBookingAsync(request.BookingId, createdBy, cancellationToken);
        if (bookingRows.Count == 0)
        {
            return Result.Failure<CreatePlayerMatchResponse>(new Error("PLAYER_MATCH.BOOKING_NOT_FOUND", "Booking not found or unavailable."));
        }

        var booking = bookingRows[0];
        if (booking.Status is not ("PENDING" or "CONFIRMED"))
        {
            return Result.Failure<CreatePlayerMatchResponse>(new Error("PLAYER_MATCH.BOOKING_INVALID_STATUS", "The booking cannot be used to create a player match."));
        }

        if (request.BranchId != booking.BranchId)
        {
            return Result.Failure<CreatePlayerMatchResponse>(new Error("PLAYER_MATCH.BOOKING_BRANCH_MISMATCH", "The selected branch does not match the booking."));
        }

        if (!bookingRows.Any(row =>
                row.BookingDate == request.MatchDate
                && row.StartTime == request.StartTime
                && row.EndTime == request.EndTime
                && (request.CourtId is null || row.CourtId == request.CourtId.Value)))
        {
            return Result.Failure<CreatePlayerMatchResponse>(new Error("PLAYER_MATCH.BOOKING_SESSION_MISMATCH", "The player match schedule and court must match a booking session."));
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
        connection.Open();
        using var transaction = connection.BeginTransaction();

        var matchId = await repository.CreateAsync(request, createdBy, transaction, cancellationToken);
        await repository.CreateParticipantAsync(matchId, createdBy, transaction, cancellationToken);
        transaction.Commit();

        return Result.Success(new CreatePlayerMatchResponse
        {
            Id = matchId,
            Message = "Player match created successfully."
        });
    }

    public async Task<Result<PlayerMatchLifecycleResponse>> CreateDraftAsync(
        PlayerMatchDraftRequest request,
        Guid createdBy,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return Result.Failure<PlayerMatchLifecycleResponse>(
                new Error("PLAYER_MATCH.INVALID_REQUEST", "Request cannot be null."));
        }

        if (createdBy == Guid.Empty)
        {
            return Result.Failure<PlayerMatchLifecycleResponse>(InvalidUser);
        }

        var normalizedRequest = NormalizeDraftRequest(request);
        var validation = await draftValidator.ValidateAsync(normalizedRequest, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<PlayerMatchLifecycleResponse>(
                new Error("PLAYER_MATCH.VALIDATION", string.Join("; ", validation.Errors.Select(error => error.ErrorMessage))));
        }

        if (!await repository.BranchExistsAsync(normalizedRequest.BranchId, cancellationToken))
        {
            return Result.Failure<PlayerMatchLifecycleResponse>(
                new Error("PLAYER_MATCH.BRANCH_NOT_FOUND", "Branch not found or unavailable."));
        }

        if (normalizedRequest.CourtId is Guid courtId
            && !await repository.CourtExistsAsync(courtId, normalizedRequest.BranchId, cancellationToken))
        {
            return Result.Failure<PlayerMatchLifecycleResponse>(
                new Error("PLAYER_MATCH.COURT_NOT_FOUND", "Court not found for the selected branch."));
        }

        var bookingValidationError = await ValidateDraftBookingAsync(normalizedRequest, createdBy, cancellationToken);
        if (bookingValidationError is not null)
        {
            return Result.Failure<PlayerMatchLifecycleResponse>(bookingValidationError);
        }

        using var connection = sqlConnectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        var matchId = await repository.CreateDraftAsync(normalizedRequest, createdBy, transaction, cancellationToken);
        await repository.CreateParticipantAsync(matchId, createdBy, transaction, cancellationToken);
        transaction.Commit();

        return Result.Success(new PlayerMatchLifecycleResponse
        {
            Id = matchId,
            Status = PlayerMatchStatus.Draft,
            Message = "Player match draft created successfully."
        });
    }

    public async Task<Result<PlayerMatchLifecycleResponse>> UpdateDraftAsync(
        Guid matchId,
        PlayerMatchDraftRequest request,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        if (matchId == Guid.Empty)
        {
            return Result.Failure<PlayerMatchLifecycleResponse>(InvalidMatchId);
        }

        if (currentUserId == Guid.Empty)
        {
            return Result.Failure<PlayerMatchLifecycleResponse>(InvalidUser);
        }

        if (request is null)
        {
            return Result.Failure<PlayerMatchLifecycleResponse>(
                new Error("PLAYER_MATCH.INVALID_REQUEST", "Request cannot be null."));
        }

        var normalizedRequest = NormalizeDraftRequest(request);
        var validation = await draftValidator.ValidateAsync(normalizedRequest, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<PlayerMatchLifecycleResponse>(
                new Error("PLAYER_MATCH.VALIDATION", string.Join("; ", validation.Errors.Select(error => error.ErrorMessage))));
        }

        if (!await repository.BranchExistsAsync(normalizedRequest.BranchId, cancellationToken))
        {
            return Result.Failure<PlayerMatchLifecycleResponse>(
                new Error("PLAYER_MATCH.BRANCH_NOT_FOUND", "Branch not found or unavailable."));
        }

        if (normalizedRequest.CourtId is Guid courtId
            && !await repository.CourtExistsAsync(courtId, normalizedRequest.BranchId, cancellationToken))
        {
            return Result.Failure<PlayerMatchLifecycleResponse>(
                new Error("PLAYER_MATCH.COURT_NOT_FOUND", "Court not found for the selected branch."));
        }

        var bookingValidationError = await ValidateDraftBookingAsync(normalizedRequest, currentUserId, cancellationToken);
        if (bookingValidationError is not null)
        {
            return Result.Failure<PlayerMatchLifecycleResponse>(bookingValidationError);
        }

        using var connection = sqlConnectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        var match = await repository.LockMatchForLifecycleAsync(matchId, transaction, cancellationToken);
        if (match is null)
        {
            return Result.Failure<PlayerMatchLifecycleResponse>(
                new Error("MATCH_ROOM_NOT_FOUND", "The player match was not found."));
        }

        if (match.CreatedBy != currentUserId)
        {
            return Result.Failure<PlayerMatchLifecycleResponse>(
                new Error("MATCH_ROOM_NOT_OWNED", "Only the host can edit this draft."));
        }

        if (!string.Equals(match.Status, PlayerMatchStatus.Draft, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure<PlayerMatchLifecycleResponse>(
                new Error("MATCH_ROOM_INVALID_STATUS_TRANSITION", "Only a draft can be edited."));
        }

        if (!await repository.UpdateDraftAsync(matchId, normalizedRequest, currentUserId, transaction, cancellationToken))
        {
            return Result.Failure<PlayerMatchLifecycleResponse>(
                new Error("MATCH_ROOM_INVALID_STATUS_TRANSITION", "The draft changed before it could be updated."));
        }

        transaction.Commit();

        return Result.Success(new PlayerMatchLifecycleResponse
        {
            Id = matchId,
            Status = PlayerMatchStatus.Draft,
            Message = "Player match draft updated successfully."
        });
    }

    public async Task<Result<PlayerMatchLifecycleResponse>> PublishDraftAsync(
        Guid matchId,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        if (matchId == Guid.Empty)
        {
            return Result.Failure<PlayerMatchLifecycleResponse>(InvalidMatchId);
        }

        if (currentUserId == Guid.Empty)
        {
            return Result.Failure<PlayerMatchLifecycleResponse>(InvalidUser);
        }

        using var connection = sqlConnectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        var match = await repository.LockMatchForLifecycleAsync(matchId, transaction, cancellationToken);
        if (match is null)
        {
            return Result.Failure<PlayerMatchLifecycleResponse>(
                new Error("MATCH_ROOM_NOT_FOUND", "The player match was not found."));
        }

        if (match.CreatedBy != currentUserId)
        {
            return Result.Failure<PlayerMatchLifecycleResponse>(
                new Error("MATCH_ROOM_NOT_OWNED", "Only the host can publish this draft."));
        }

        if (!string.Equals(match.Status, PlayerMatchStatus.Draft, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure<PlayerMatchLifecycleResponse>(
                new Error("MATCH_ROOM_INVALID_STATUS_TRANSITION", "Only a draft can be published."));
        }

        if (match.BookingId is null
            || match.CourtId is null
            || match.MatchDate is null
            || match.StartTime is null
            || match.EndTime is null
            || match.GameType is null
            || match.MinimumRating is null
            || match.MaximumRating is null
            || match.GenderPolicy is null
            || match.ParticipationFee is null
            || (match.GenderPolicy == "SPECIFIC" && match.RequiredGender is null))
        {
            return Result.Failure<PlayerMatchLifecycleResponse>(
                new Error("MATCH_ROOM_DRAFT_INCOMPLETE", "Complete the required room details and booking schedule before publishing."));
        }

        var activeParticipants = await repository.CountActiveParticipantsAsync(matchId, transaction, cancellationToken);
        if (activeParticipants != match.CurrentPlayers)
        {
            return Result.Failure<PlayerMatchLifecycleResponse>(
                new Error("MATCH_ROOM_CAPACITY_INCONSISTENT", "The room membership count is inconsistent."));
        }

        if (activeParticipants >= match.MaximumPlayers)
        {
            return Result.Failure<PlayerMatchLifecycleResponse>(
                new Error("MATCH_ROOM_FULL", "A full room cannot be published."));
        }

        if (!await repository.IsBookingValidForPublicationAsync(match, transaction, cancellationToken))
        {
            return Result.Failure<PlayerMatchLifecycleResponse>(
                new Error("MATCH_ROOM_BOOKING_INVALID", "The confirmed booking does not cover this room schedule."));
        }

        if (await repository.HasOverlappingPublishedMatchAsync(match, transaction, cancellationToken))
        {
            return Result.Failure<PlayerMatchLifecycleResponse>(
                new Error("MATCH_ROOM_SCHEDULE_CONFLICT", "This room overlaps another published room."));
        }

        if (!await repository.PublishDraftAsync(matchId, currentUserId, transaction, cancellationToken))
        {
            return Result.Failure<PlayerMatchLifecycleResponse>(
                new Error("MATCH_ROOM_INVALID_STATUS_TRANSITION", "The draft changed before it could be published."));
        }

        transaction.Commit();

        return Result.Success(new PlayerMatchLifecycleResponse
        {
            Id = matchId,
            Status = PlayerMatchStatus.Open,
            Message = "Player match published successfully."
        });
    }

    public async Task<Result<PlayerMatchLifecycleResponse>> CancelMatchAsync(
        Guid matchId,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        if (matchId == Guid.Empty)
        {
            return Result.Failure<PlayerMatchLifecycleResponse>(InvalidMatchId);
        }

        if (currentUserId == Guid.Empty)
        {
            return Result.Failure<PlayerMatchLifecycleResponse>(InvalidUser);
        }

        using var connection = sqlConnectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        var match = await repository.LockMatchForLifecycleAsync(matchId, transaction, cancellationToken);
        if (match is null)
        {
            return Result.Failure<PlayerMatchLifecycleResponse>(
                new Error("MATCH_ROOM_NOT_FOUND", "The player match was not found."));
        }

        if (match.CreatedBy != currentUserId)
        {
            return Result.Failure<PlayerMatchLifecycleResponse>(
                new Error("MATCH_ROOM_NOT_OWNED", "Only the host can cancel this room."));
        }

        if (!new[] { PlayerMatchStatus.Draft, PlayerMatchStatus.Open, PlayerMatchStatus.Full }
                .Contains(match.Status, StringComparer.OrdinalIgnoreCase))
        {
            return Result.Failure<PlayerMatchLifecycleResponse>(
                new Error("MATCH_ROOM_INVALID_STATUS_TRANSITION", "Only a draft, open, or full room can be cancelled."));
        }

        if (!await repository.CancelMatchAsync(matchId, currentUserId, transaction, cancellationToken))
        {
            return Result.Failure<PlayerMatchLifecycleResponse>(
                new Error("MATCH_ROOM_INVALID_STATUS_TRANSITION", "The room changed before it could be cancelled."));
        }

        await repository.ExpirePendingJoinRequestsAsync(matchId, transaction, cancellationToken);
        transaction.Commit();

        return Result.Success(new PlayerMatchLifecycleResponse
        {
            Id = matchId,
            Status = PlayerMatchStatus.Cancelled,
            Message = "Player match cancelled successfully."
        });
    }

    public async Task<Result<JoinMatchResponse>> JoinMatchAsync(
        Guid matchId,
        Guid currentUserId,
        string? message,
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

        if (message?.Length > 500)
        {
            return Result.Failure<JoinMatchResponse>(new Error("PLAYER_MATCH.INVALID_REQUEST", "Join request message cannot exceed 500 characters."));
        }

        message = NormalizeText(message);

        using var connection = sqlConnectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        var match = await repository.LockMatchForJoinAsync(matchId, transaction, cancellationToken);
        if (match is null)
        {
            return Result.Failure<JoinMatchResponse>(new Error("PLAYER_MATCH.NOT_FOUND", "Player match not found."));
        }

        if (IsUncompletedPublishedStatus(match.Status)
            && !await repository.IsBookingValidAsync(match, transaction, cancellationToken))
        {
            if (!await CancelInvalidBookingRoomAsync(matchId, transaction, cancellationToken))
            {
                return Result.Failure<JoinMatchResponse>(
                    new Error("MATCH_ROOM_INVALID_STATUS_TRANSITION", "The room changed while its booking was being checked."));
            }

            transaction.Commit();
            return Result.Failure<JoinMatchResponse>(
                new Error("MATCH_ROOM_BOOKING_INVALID", "The confirmed booking linked to this room is no longer valid."));
        }

        if (!string.Equals(match.Status, PlayerMatchStatus.Open, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure<JoinMatchResponse>(new Error("PLAYER_MATCH.NOT_OPEN", "Match is not open for joining."));
        }

        if (!match.HasNotStarted)
        {
            return Result.Failure<JoinMatchResponse>(new Error("PLAYER_MATCH.MATCH_EXPIRED", "Expired matches cannot be joined."));
        }

        if (match.HostUserId == currentUserId)
        {
            return Result.Failure<JoinMatchResponse>(
                new Error("PLAYER_MATCH.HOST_ALREADY_PARTICIPANT", "Host is already a participant."));
        }

        var activePlayerCount = await repository.CountActiveParticipantsAsync(matchId, transaction, cancellationToken);
        if (activePlayerCount != match.CurrentPlayers)
        {
            return Result.Failure<JoinMatchResponse>(
                new Error("MATCH_ROOM_CAPACITY_INCONSISTENT", "Room capacity requires operator reconciliation."));
        }

        if (activePlayerCount >= match.MaximumPlayers)
        {
            return Result.Failure<JoinMatchResponse>(
                new Error("PLAYER_MATCH.MATCH_FULL", "Match is already full."));
        }

        if (await repository.IsActiveParticipantAsync(matchId, currentUserId, transaction, cancellationToken))
        {
            return Result.Failure<JoinMatchResponse>(new Error("PLAYER_MATCH.ALREADY_JOINED", "You have already joined this match."));
        }

        if (await repository.HasPendingJoinRequestAsync(matchId, currentUserId, transaction, cancellationToken))
        {
            return Result.Failure<JoinMatchResponse>(new Error("PLAYER_MATCH.REQUEST_EXISTS", "Join request already exists."));
        }

        var joinRequestId = await repository.CreateJoinRequestAsync(matchId, currentUserId, message, transaction, cancellationToken);
        if (joinRequestId is null)
        {
            return Result.Failure<JoinMatchResponse>(new Error(
                "PLAYER_MATCH.REQUEST_STATE_CHANGED",
                "The match is no longer available for joining, or a request already exists."));
        }

        transaction.Commit();

        await PublishNotificationBestEffortAsync(
            notificationCancellationToken => repository.CreateNotificationAsync(
                match.HostUserId,
                matchId,
                "JOIN_REQUEST",
                "Join request",
                "A player requested to join your match.",
                notificationCancellationToken),
            "Could not create the host notification for join request {JoinRequestId}.",
            joinRequestId.Value);

        return Result.Success(new JoinMatchResponse
        {
            MatchId = matchId,
            JoinRequestId = joinRequestId.Value,
            Status = "PENDING",
            Message = "Join request submitted successfully."
        });
    }

    public async Task<Result<JoinMatchResponse>> CancelJoinRequestAsync(
        Guid matchId,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        if (matchId == Guid.Empty)
        {
            return Result.Failure<JoinMatchResponse>(InvalidMatchId);
        }

        if (currentUserId == Guid.Empty)
        {
            return Result.Failure<JoinMatchResponse>(InvalidUser);
        }

        var cancelledRequestId = await repository.CancelJoinRequestAsync(matchId, currentUserId, cancellationToken);
        if (cancelledRequestId is null)
        {
            return Result.Failure<JoinMatchResponse>(
                new Error("PLAYER_MATCH.REQUEST_NOT_PENDING", "No pending join request is available to cancel."));
        }

        return Result.Success(new JoinMatchResponse
        {
            MatchId = matchId,
            JoinRequestId = cancelledRequestId.Value,
            Status = JoinRequestStatus.Cancelled,
            Message = "Join request cancelled successfully."
        });
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

        using var connection = sqlConnectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        var match = await repository.LockMatchForApprovalAsync(matchId, transaction, cancellationToken);
        if (match is null)
        {
            return Result.Failure<ApproveJoinRequestResponse>(
                new Error("MATCH_ROOM_NOT_FOUND", "Player match not found."));
        }

        if (match.HostUserId != currentUserId)
        {
            return Result.Failure<ApproveJoinRequestResponse>(
                new Error("MATCH_ROOM_NOT_OWNED", "Only the host can approve join requests."));
        }

        var joinRequest = await repository.LockJoinRequestForApprovalAsync(
            matchId, requestId, transaction, cancellationToken);
        if (joinRequest is null)
        {
            return Result.Failure<ApproveJoinRequestResponse>(
                new Error("JOIN_REQUEST_NOT_FOUND", "Join request was not found."));
        }

        if (string.Equals(match.Status, PlayerMatchStatus.Full, StringComparison.OrdinalIgnoreCase)
            && joinRequest.Status is JoinRequestStatus.Pending or JoinRequestStatus.Expired)
        {
            return Result.Failure<ApproveJoinRequestResponse>(
                new Error("MATCH_ROOM_FULL", "Match is already full."));
        }

        if (IsUncompletedPublishedStatus(match.Status)
            && !await repository.IsBookingValidAsync(match, transaction, cancellationToken))
        {
            if (!await CancelInvalidBookingRoomAsync(matchId, transaction, cancellationToken))
            {
                return Result.Failure<ApproveJoinRequestResponse>(
                    new Error("MATCH_ROOM_INVALID_STATUS_TRANSITION", "The room changed while its booking was being checked."));
            }

            transaction.Commit();
            return Result.Failure<ApproveJoinRequestResponse>(
                new Error("MATCH_ROOM_BOOKING_INVALID", "The match booking is no longer valid."));
        }

        if (!string.Equals(joinRequest.Status, JoinRequestStatus.Pending, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure<ApproveJoinRequestResponse>(
                new Error("JOIN_REQUEST_INVALID_STATUS", "Only pending join requests can be approved."));
        }

        if (!string.Equals(match.Status, PlayerMatchStatus.Open, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure<ApproveJoinRequestResponse>(
                new Error("MATCH_ROOM_INVALID_STATUS_TRANSITION", "Match is not open for new approvals."));
        }

        if (!match.HasNotStarted)
        {
            return Result.Failure<ApproveJoinRequestResponse>(
                new Error("MATCH_ROOM_INVALID_STATUS_TRANSITION", "Approvals stop when the match starts."));
        }

        if (!await repository.IsPlayerEligibleForApprovalAsync(
                match, joinRequest.UserId, transaction, cancellationToken))
        {
            return Result.Failure<ApproveJoinRequestResponse>(
                new Error("PLAYER_NOT_ELIGIBLE", "The applicant is not eligible to join this match."));
        }

        var activePlayerCount = await repository.CountActiveParticipantsAsync(matchId, transaction, cancellationToken);
        if (activePlayerCount != match.CurrentPlayers)
        {
            return Result.Failure<ApproveJoinRequestResponse>(
                new Error("MATCH_ROOM_CAPACITY_INCONSISTENT", "Room capacity requires operator reconciliation."));
        }

        if (activePlayerCount >= match.MaximumPlayers)
        {
            return Result.Failure<ApproveJoinRequestResponse>(
                new Error("MATCH_ROOM_FULL", "Match is already full."));
        }

        if (await repository.IsActiveParticipantAsync(matchId, joinRequest.UserId, transaction, cancellationToken))
        {
            return Result.Failure<ApproveJoinRequestResponse>(
                new Error("MATCH_ROOM_ALREADY_MEMBER", "The applicant is already a member of this match."));
        }

        var updatedPlayerCount = await repository.TryIncrementCurrentPlayersAsync(matchId, transaction, cancellationToken);
        if (updatedPlayerCount is null)
        {
            return Result.Failure<ApproveJoinRequestResponse>(new Error(
                "MATCH_ROOM_INVALID_STATUS_TRANSITION",
                "The match is no longer open for approval."));
        }

        if (!await repository.ApproveJoinRequestAsync(matchId, requestId, currentUserId, transaction, cancellationToken))
        {
            return Result.Failure<ApproveJoinRequestResponse>(
                new Error("JOIN_REQUEST_INVALID_STATUS", "Only pending join requests can be approved."));
        }

        await repository.CreateParticipantAsync(matchId, joinRequest.UserId, "PLAYER", transaction, cancellationToken);

        if (updatedPlayerCount.Value >= match.MaximumPlayers)
        {
            await repository.UpdateMatchStatusAsync(matchId, "FULL", transaction, cancellationToken);
            await repository.ExpirePendingJoinRequestsAsync(matchId, transaction, cancellationToken);
        }

        transaction.Commit();

        await PublishNotificationBestEffortAsync(
            notificationCancellationToken => repository.CreateNotificationAsync(
                joinRequest.UserId,
                matchId,
                "JOIN_APPROVED",
                "Join request approved",
                "Your join request was approved by the match host.",
                notificationCancellationToken),
            "Could not create approval notification for request {JoinRequestId}.",
            requestId);

        if (updatedPlayerCount.Value >= match.MaximumPlayers)
        {
            await PublishNotificationBestEffortAsync(
                notificationCancellationToken => repository.CreateMatchFullNotificationsAsync(
                    matchId, notificationCancellationToken),
                "Could not create full-room notifications for match {MatchId}.",
                matchId);
        }

        return Result.Success(new ApproveJoinRequestResponse
        {
            MatchId = matchId,
            RequestId = requestId,
            UserId = joinRequest.UserId,
            Status = "APPROVED",
            Message = "Join request approved successfully."
        });
    }

    public async Task<Result<RejectJoinRequestResponse>> RejectJoinRequestAsync(
        Guid matchId,
        Guid requestId,
        Guid currentUserId,
        string? reason,
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

        if (reason?.Length > 500)
        {
            return Result.Failure<RejectJoinRequestResponse>(new Error("PLAYER_MATCH.INVALID_REQUEST", "Rejection reason cannot exceed 500 characters."));
        }

        reason = NormalizeText(reason);

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
        connection.Open();
        using var transaction = connection.BeginTransaction();

        var rejected = await repository.RejectJoinRequestAsync(
            matchId, requestId, currentUserId, reason, transaction, cancellationToken);
        if (!rejected)
        {
            transaction.Rollback();
            return Result.Failure<RejectJoinRequestResponse>(new Error(
                "PLAYER_MATCH.REQUEST_STATE_CHANGED",
                "The match or join request changed. Refresh and try again."));
        }

        transaction.Commit();

        await PublishNotificationBestEffortAsync(
            notificationCancellationToken => repository.CreateNotificationAsync(
                joinRequest.UserId,
                matchId,
                "JOIN_REJECTED",
                "Join request rejected",
                "Your request to join the match has been rejected.",
                notificationCancellationToken),
            "Could not create rejection notification for request {JoinRequestId}.",
            requestId);

        return Result.Success(new RejectJoinRequestResponse
        {
            MatchId = matchId,
            RequestId = requestId,
            UserId = joinRequest.UserId,
            Status = "REJECTED",
            Message = "Join request rejected successfully."
        });
    }

    private async Task<Result<ResolvedScoringCriteria>> ResolveScoringCriteriaAsync(
        PlayerMatchScoringCriteriaRequest? request,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return Result.Failure<ResolvedScoringCriteria>(
                new Error("SEARCH_CRITERIA_INVALID", "Search criteria cannot be null."));
        }

        var criteria = NormalizeScoringCriteria(request);
        var validation = await scoringCriteriaValidator.ValidateAsync(criteria, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<ResolvedScoringCriteria>(
                new Error("SEARCH_CRITERIA_INVALID", string.Join("; ", validation.Errors.Select(error => error.ErrorMessage))));
        }

        if (criteria.Preferences is not null && MatchScoreCalculator.GetWeightTotal(criteria.Preferences) != 100m)
        {
            return Result.Failure<ResolvedScoringCriteria>(
                new Error("PREFERENCE_WEIGHT_TOTAL_INVALID", "Preference weights must total exactly 100."));
        }

        if (criteria.BranchIds.Count > 0 && !await repository.BranchesExistAsync(criteria.BranchIds, cancellationToken))
        {
            return Result.Failure<ResolvedScoringCriteria>(
                new Error("SEARCH_CRITERIA_INVALID", "One or more selected branches are unavailable."));
        }

        var profileResult = await profileService.GetProfileAsync(currentUserId, cancellationToken);
        if (profileResult.IsFailure && profileResult.Error.Code != "PLAYER_MATCHING_PROFILE_REQUIRED")
        {
            return Result.Failure<ResolvedScoringCriteria>(profileResult.Error);
        }

        var profile = profileResult.IsSuccess ? profileResult.Value : null;
        if (profile is { IsMatchingEnabled: false })
        {
            return Result.Failure<ResolvedScoringCriteria>(
                new Error("PLAYER_MATCHING_DISABLED", "Enable player matching before searching."));
        }

        var weights = criteria.Preferences ?? new PlayerMatchPreferencesResponse().Preferences;
        if (criteria.Preferences is null && profile is not null)
        {
            var preferencesResult = await profileService.GetPreferencesAsync(currentUserId, cancellationToken);
            if (preferencesResult.IsFailure)
            {
                return Result.Failure<ResolvedScoringCriteria>(preferencesResult.Error);
            }

            weights = preferencesResult.Value.Preferences;
        }

        if (MatchScoreCalculator.GetWeightTotal(weights) != 100m)
        {
            return Result.Failure<ResolvedScoringCriteria>(
                new Error("PREFERENCE_WEIGHT_TOTAL_INVALID", "Preference weights must total exactly 100."));
        }

        if (!MatchScoreCalculator.AreWeightsValid(weights))
        {
            return Result.Failure<ResolvedScoringCriteria>(
                new Error("SEARCH_CRITERIA_INVALID", "Preference weights must be between 0 and 100."));
        }

        var effectiveCriteria = new PlayerMatchScoringCriteriaRequest
        {
            BranchIds = criteria.BranchIds,
            Keyword = criteria.Keyword,
            SkillLevel = criteria.SkillLevel,
            GenderPreference = criteria.GenderPreference,
            StartTime = criteria.StartTime,
            EndTime = criteria.EndTime,
            MatchDate = criteria.MatchDate,
            PreferredStartTime = criteria.PreferredStartTime,
            PreferredEndTime = criteria.PreferredEndTime,
            MinimumTimeOverlapMinutes = criteria.MinimumTimeOverlapMinutes,
            GameTypes = criteria.GameTypes.Count == 0 ? ["SINGLES", "DOUBLES"] : criteria.GameTypes,
            MaximumParticipationFee = criteria.MaximumParticipationFee,
            Preferences = weights
        };

        return Result.Success(new ResolvedScoringCriteria(effectiveCriteria, profile, weights));
    }

    private static PlayerMatchScoringCriteriaRequest NormalizeScoringCriteria(
        PlayerMatchScoringCriteriaRequest request) =>
        new()
        {
            BranchIds = request.BranchIds?.ToArray() ?? [],
            Keyword = NormalizeText(request.Keyword),
            SkillLevel = NormalizeText(request.SkillLevel)?.ToUpperInvariant(),
            GenderPreference = NormalizeText(request.GenderPreference)?.ToUpperInvariant(),
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            MatchDate = request.MatchDate,
            PreferredStartTime = request.PreferredStartTime,
            PreferredEndTime = request.PreferredEndTime,
            MinimumTimeOverlapMinutes = request.MinimumTimeOverlapMinutes,
            GameTypes = request.GameTypes?
                .Select(gameType => gameType?.Trim().ToUpperInvariant() ?? string.Empty)
                .ToArray() ?? [],
            MaximumParticipationFee = request.MaximumParticipationFee,
            Preferences = request.Preferences is null ? null : CopyWeights(request.Preferences)
        };

    private static ScoredPlayerMatch ScoreCandidate(
        PersonalizedMatchCandidate candidate,
        PlayerMatchingProfileResponse? profile,
        PlayerMatchPreferenceWeights weights,
        PlayerMatchScoringCriteriaRequest criteria)
    {
        var userLevel = criteria.SkillLevel ?? (profile is null ? null : SkillLevels.FromRating(profile.CurrentRating));
        var searchStart = criteria.StartTime ?? criteria.PreferredStartTime;
        var searchEnd = criteria.EndTime ?? criteria.PreferredEndTime;
        var roomGender = candidate.GenderPolicy switch
        {
            "ANY" => "ANY",
            "SPECIFIC" => candidate.RequiredGender,
            _ => candidate.GenderPreference
        };

        var skillScore = MatchScoreCalculator.CalculateSkillScore(userLevel, candidate.SkillLevel);
        var timeScore = MatchScoreCalculator.CalculateTimeScore(searchStart, searchEnd, candidate.StartTime, candidate.EndTime);
        var branchScore = MatchScoreCalculator.CalculateBranchScore(criteria.BranchIds.FirstOrDefault(), candidate.BranchId);
        var genderScore = MatchScoreCalculator.CalculateGenderScore(profile?.Gender ?? criteria.GenderPreference, roomGender);
        var rawMatchScore = MatchScoreCalculator.CalculateMatchScore(skillScore, timeScore, branchScore, genderScore, weights);

        var reasons = new List<string>(3);
        var warnings = new List<string>(1);
        if (skillScore == 100m)
        {
            reasons.Add("SKILL_MATCH");
        }

        if (timeScore == 100m)
        {
            reasons.Add("FULL_TIME_OVERLAP");
        }
        else if (timeScore > 0m)
        {
            warnings.Add("PARTIAL_TIME_OVERLAP");
        }
        else if (searchStart.HasValue && searchEnd.HasValue)
        {
            warnings.Add("NO_TIME_OVERLAP");
        }

        if (branchScore == 100m)
        {
            reasons.Add("PRIMARY_BRANCH");
        }

        var displayedMatchScore = RoundScore(rawMatchScore);
        var response = new PersonalizedPlayerMatchResponse
        {
            RoomId = candidate.RoomId,
            RoomCode = candidate.RoomCode,
            Title = candidate.Title,
            BranchId = candidate.BranchId,
            BranchName = candidate.BranchName,
            CourtName = candidate.CourtName,
            CourtReservationStatus = "COURT_RESERVED",
            MatchDate = candidate.MatchDate,
            StartTime = candidate.StartTime,
            EndTime = candidate.EndTime,
            GameType = candidate.GameType,
            MinimumRating = candidate.MinimumRating,
            MaximumRating = candidate.MaximumRating,
            CurrentPlayerCount = candidate.CurrentPlayerCount,
            RequiredPlayerCount = candidate.RequiredPlayerCount,
            AvailableSlots = candidate.RequiredPlayerCount - candidate.CurrentPlayerCount,
            ParticipationFee = candidate.ParticipationFee,
            MatchScore = displayedMatchScore,
            MatchLevel = displayedMatchScore >= 85m
                ? "EXCELLENT"
                : displayedMatchScore >= 70m
                    ? "GOOD"
                    : "POSSIBLE",
            Weights = CopyWeights(weights),
            ScoreBreakdown = new PlayerMatchScoreBreakdown
            {
                SkillScore = RoundScore(skillScore),
                TimeScore = timeScore < 100m ? Math.Min(RoundScore(timeScore), 99.9m) : 100m,
                BranchScore = RoundScore(branchScore),
                GenderScore = RoundScore(genderScore),
                SkillContribution = RoundScore(MatchScoreCalculator.CalculateContribution(skillScore, weights.SkillWeight)),
                TimeContribution = RoundScore(MatchScoreCalculator.CalculateContribution(timeScore, weights.TimeWeight)),
                BranchContribution = RoundScore(MatchScoreCalculator.CalculateContribution(branchScore, weights.BranchWeight)),
                GenderContribution = RoundScore(MatchScoreCalculator.CalculateContribution(genderScore, weights.GenderWeight))
            },
            Reasons = reasons,
            Warnings = warnings,
            JoinRequestStatus = null
        };

        return new ScoredPlayerMatch(response, rawMatchScore);
    }

    private static decimal RoundScore(decimal score) =>
        decimal.Round(score, 1, MidpointRounding.AwayFromZero);

    private static PlayerMatchPreferenceWeights CopyWeights(PlayerMatchPreferenceWeights weights) =>
        new()
        {
            SkillWeight = weights.SkillWeight,
            TimeWeight = weights.TimeWeight,
            BranchWeight = weights.BranchWeight,
            GenderWeight = weights.GenderWeight
        };

    private sealed record ResolvedScoringCriteria(
        PlayerMatchScoringCriteriaRequest Criteria,
        PlayerMatchingProfileResponse? Profile,
        PlayerMatchPreferenceWeights Weights);

    private sealed record ScoredPlayerMatch(
        PersonalizedPlayerMatchResponse Response,
        decimal RawMatchScore);

    private async Task<bool> CancelInvalidBookingRoomAsync(
        Guid matchId,
        IDbTransaction transaction,
        CancellationToken cancellationToken)
    {
        if (!await repository.CancelInvalidatedMatchAsync(matchId, transaction, cancellationToken))
        {
            return false;
        }

        await repository.ExpirePendingJoinRequestsAsync(matchId, transaction, cancellationToken);
        return true;
    }

    private async Task PublishNotificationBestEffortAsync(
        Func<CancellationToken, Task> publish,
        string logMessage,
        Guid entityId)
    {
        try
        {
            await publish(CancellationToken.None);
        }
        catch (DbException exception)
        {
            logger.LogWarning(exception, logMessage, entityId);
        }
        catch (TimeoutException exception)
        {
            logger.LogWarning(exception, logMessage, entityId);
        }
    }

    private static string? NormalizeText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool IsUncompletedPublishedStatus(string status) =>
        string.Equals(status, PlayerMatchStatus.Open, StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, PlayerMatchStatus.Full, StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, PlayerMatchStatus.Started, StringComparison.OrdinalIgnoreCase);

    private async Task<Error?> ValidateDraftBookingAsync(
        PlayerMatchDraftRequest request,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        if (request.BookingId is not Guid bookingId)
        {
            return null;
        }

        var bookingRows = await bookingRepository.GetPlayerMatchBookingAsync(bookingId, currentUserId, cancellationToken);
        if (bookingRows.Count == 0)
        {
            return new Error("PLAYER_MATCH.BOOKING_NOT_FOUND", "Booking not found or unavailable.");
        }

        if (bookingRows.All(row => row.BranchId != request.BranchId))
        {
            return new Error("PLAYER_MATCH.BOOKING_BRANCH_MISMATCH", "The selected branch does not match the booking.");
        }

        return null;
    }

    private static PlayerMatchDraftRequest NormalizeDraftRequest(PlayerMatchDraftRequest request) =>
        new()
        {
            BookingId = request.BookingId,
            BranchId = request.BranchId,
            CourtId = request.CourtId,
            Title = request.Title?.Trim() ?? string.Empty,
            Description = NormalizeText(request.Description),
            MatchDate = request.MatchDate,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            GameType = request.GameType?.Trim().ToUpperInvariant() ?? string.Empty,
            MinimumRating = request.MinimumRating,
            MaximumRating = request.MaximumRating,
            RequiredPlayerCount = request.RequiredPlayerCount,
            GenderPolicy = request.GenderPolicy?.Trim().ToUpperInvariant() ?? string.Empty,
            RequiredGender = NormalizeText(request.RequiredGender)?.ToUpperInvariant(),
            ParticipationFee = request.ParticipationFee
        };

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
