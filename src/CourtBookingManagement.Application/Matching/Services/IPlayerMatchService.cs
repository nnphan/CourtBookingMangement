using CourtBookingManagement.Application.Matching.Models;
using CourtBookingManagement.Application.Matching.Models.Requests;
using CourtBookingManagement.Application.Matching.Models.Responses;
using CourtBookingManagement.Domain.Abstractions;

namespace CourtBookingManagement.Application.Matching.Services;

public interface IPlayerMatchService
{
    Task<Result<PagedResult<PlayerMatchResponse>>> GetMatchesAsync(PlayerMatchSearchRequest request, CancellationToken cancellationToken);

    Task<Result<PersonalizedPlayerMatchSearchResult>> SearchPersonalizedAsync(
        PersonalizedPlayerMatchSearchRequest request,
        Guid currentUserId,
        CancellationToken cancellationToken);

    Task<Result<PersonalizedPlayerMatchResponse>> ScoreMatchAsync(
        Guid matchId,
        PlayerMatchScoringCriteriaRequest request,
        Guid currentUserId,
        CancellationToken cancellationToken);

    Task<Result<PlayerMatchDetailResponse>> GetMatchByIdAsync(Guid matchId, Guid? viewerUserId, CancellationToken cancellationToken);

    Task<Result<PagedResult<HostedPlayerMatchResponse>>> GetHostedMatchesAsync(GetHostedMatchesRequest request, Guid currentUserId, CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<PlayerMatchJoinRequestResponse>>> GetJoinRequestsAsync(Guid matchId, GetPlayerMatchJoinRequestsRequest request, Guid currentUserId, CancellationToken cancellationToken);

    Task<Result<PagedResult<MyMatchJoinRequestResponse>>> GetMyJoinRequestsAsync(
        GetMyMatchJoinRequestsRequest request,
        Guid currentUserId,
        CancellationToken cancellationToken);

    Task<Result<PagedResult<ParticipatingPlayerMatchResponse>>> GetParticipatingMatchesAsync(GetParticipatingMatchesRequest request, Guid currentUserId, CancellationToken cancellationToken);

    Task<Result<CreatePlayerMatchResponse>> CreateAsync(CreatePlayerMatchRequest request, Guid createdBy, CancellationToken cancellationToken);

    Task<Result<PlayerMatchLifecycleResponse>> CreateDraftAsync(
        PlayerMatchDraftRequest request,
        Guid createdBy,
        CancellationToken cancellationToken);

    Task<Result<PlayerMatchLifecycleResponse>> UpdateDraftAsync(
        Guid matchId,
        PlayerMatchDraftRequest request,
        Guid currentUserId,
        CancellationToken cancellationToken);

    Task<Result<PlayerMatchLifecycleResponse>> PublishDraftAsync(
        Guid matchId,
        Guid currentUserId,
        CancellationToken cancellationToken);

    Task<Result<PlayerMatchLifecycleResponse>> CancelMatchAsync(
        Guid matchId,
        Guid currentUserId,
        CancellationToken cancellationToken);

    Task<Result<JoinMatchResponse>> JoinMatchAsync(
        Guid matchId,
        Guid currentUserId,
        string? message,
        CancellationToken cancellationToken);

    Task<Result<JoinMatchResponse>> CancelJoinRequestAsync(Guid matchId, Guid currentUserId, CancellationToken cancellationToken);

    Task<Result<ApproveJoinRequestResponse>> ApproveJoinRequestAsync(Guid matchId, Guid requestId, Guid currentUserId, CancellationToken cancellationToken);

    Task<Result<RejectJoinRequestResponse>> RejectJoinRequestAsync(
        Guid matchId,
        Guid requestId,
        Guid currentUserId,
        string? reason,
        CancellationToken cancellationToken);
}
