using System.Data;
using CourtBookingManagement.Application.Matching.Models;
using CourtBookingManagement.Application.Matching.Models.Requests;
using CourtBookingManagement.Application.Matching.Models.Responses;

namespace CourtBookingManagement.Application.Matching.Repositories;

public interface IPlayerMatchRepository
{
    Task<PagedResult<PlayerMatchResponse>> SearchAsync(PlayerMatchSearchRequest request, CancellationToken cancellationToken);

    Task<PlayerMatchDetailResponse?> GetByIdAsync(Guid matchId, CancellationToken cancellationToken);

    Task<PlayerMatchDetailResponse?> GetMatchByIdAsync(Guid matchId, CancellationToken cancellationToken);

    Task<PlayerMatchJoinRequest?> GetJoinRequestAsync(Guid requestId, CancellationToken cancellationToken);

    Task<bool> RejectJoinRequestAsync(Guid requestId, IDbTransaction transaction, CancellationToken cancellationToken);

    Task<bool> BranchExistsAsync(Guid branchId, CancellationToken cancellationToken);

    Task<bool> CourtExistsAsync(Guid courtId, Guid branchId, CancellationToken cancellationToken);

    Task<bool> HasOverlappingMatchAsync(Guid? courtId, DateOnly matchDate, TimeOnly startTime, TimeOnly endTime, CancellationToken cancellationToken);

    Task<bool> DuplicateMatchExistsAsync(Guid createdBy, Guid? courtId, DateOnly matchDate, TimeOnly startTime, TimeOnly endTime, CancellationToken cancellationToken);

    Task<bool> IsParticipantAsync(Guid matchId, Guid userId, CancellationToken cancellationToken);

    Task<bool> HasPendingJoinRequestAsync(Guid matchId, Guid userId, CancellationToken cancellationToken);

    Task<Guid> CreateAsync(CreatePlayerMatchRequest request, Guid createdBy, IDbTransaction transaction, CancellationToken cancellationToken);

    Task<Guid> CreateJoinRequestAsync(Guid matchId, Guid userId, IDbTransaction transaction, CancellationToken cancellationToken);

    Task CreateParticipantAsync(Guid matchId, Guid userId, IDbTransaction transaction, CancellationToken cancellationToken);

    Task CreateParticipantAsync(Guid matchId, Guid userId, string role, IDbTransaction transaction, CancellationToken cancellationToken);

    Task ApproveJoinRequestAsync(Guid requestId, Guid currentUserId, IDbTransaction transaction, CancellationToken cancellationToken);

    Task IncrementCurrentPlayersAsync(Guid matchId, IDbTransaction transaction, CancellationToken cancellationToken);

    Task UpdateMatchStatusAsync(Guid matchId, string status, IDbTransaction transaction, CancellationToken cancellationToken);

    Task CreateNotificationAsync(Guid matchId, Guid hostUserId, string message, IDbTransaction transaction, CancellationToken cancellationToken);

    Task CreateNotificationAsync(Guid userId, Guid? matchId, string type, string title, string message, IDbTransaction transaction, CancellationToken cancellationToken);

    Task CreateMatchFullNotificationsAsync(Guid matchId, IDbTransaction transaction, CancellationToken cancellationToken);
}
