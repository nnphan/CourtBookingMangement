using System.Data;
using CourtBookingManagement.Application.Matching.Models;
using CourtBookingManagement.Application.Matching.Models.Requests;
using CourtBookingManagement.Application.Matching.Models.Responses;

namespace CourtBookingManagement.Application.Matching.Repositories;

public interface IPlayerMatchRepository
{
    Task<PagedResult<PlayerMatchResponse>> SearchAsync(PlayerMatchSearchRequest request, CancellationToken cancellationToken);

    Task<PlayerMatchDetailResponse?> GetByIdAsync(Guid matchId, CancellationToken cancellationToken);

    Task<bool> BranchExistsAsync(Guid branchId, CancellationToken cancellationToken);

    Task<bool> CourtExistsAsync(Guid courtId, Guid branchId, CancellationToken cancellationToken);

    Task<bool> HasOverlappingMatchAsync(Guid? courtId, DateOnly matchDate, TimeOnly startTime, TimeOnly endTime, CancellationToken cancellationToken);

    Task<bool> DuplicateMatchExistsAsync(Guid createdBy, Guid? courtId, DateOnly matchDate, TimeOnly startTime, TimeOnly endTime, CancellationToken cancellationToken);

    Task<Guid> CreateAsync(CreatePlayerMatchRequest request, Guid createdBy, IDbTransaction transaction, CancellationToken cancellationToken);

    Task CreateParticipantAsync(Guid matchId, Guid userId, IDbTransaction transaction, CancellationToken cancellationToken);
}
