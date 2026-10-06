using CourtBookingManagement.Application.Matching.Models;
using CourtBookingManagement.Application.Matching.Models.Requests;
using CourtBookingManagement.Application.Matching.Models.Responses;

namespace CourtBookingManagement.Application.Matching.Repositories;

public interface IPlayerMatchRepository
{
    Task<PagedResult<PlayerMatchResponse>> SearchAsync(PlayerMatchSearchRequest request, CancellationToken cancellationToken);
}
