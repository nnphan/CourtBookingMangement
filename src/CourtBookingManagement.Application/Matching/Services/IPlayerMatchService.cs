using CourtBookingManagement.Application.Matching.Models;
using CourtBookingManagement.Application.Matching.Models.Requests;
using CourtBookingManagement.Application.Matching.Models.Responses;
using CourtBookingManagement.Domain.Abstractions;

namespace CourtBookingManagement.Application.Matching.Services;

public interface IPlayerMatchService
{
    Task<Result<PagedResult<PlayerMatchResponse>>> GetMatchesAsync(PlayerMatchSearchRequest request, CancellationToken cancellationToken);

    Task<Result<PlayerMatchDetailResponse>> GetMatchByIdAsync(Guid matchId, CancellationToken cancellationToken);

    Task<Result<CreatePlayerMatchResponse>> CreateAsync(CreatePlayerMatchRequest request, Guid createdBy, CancellationToken cancellationToken);

    Task<Result<JoinMatchResponse>> JoinMatchAsync(Guid matchId, Guid currentUserId, CancellationToken cancellationToken);
}
