using CourtBookingManagement.Application.Matching.Models;
using CourtBookingManagement.Application.Matching.Models.Requests;
using CourtBookingManagement.Application.Matching.Models.Responses;

namespace CourtBookingManagement.Application.Matching.Repositories;

public interface IMyMatchRepository
{
    Task<PagedResult<MyMatchResponse>> GetMyMatchesAsync(
        Guid userId,
        MyMatchSearchRequest request,
        CancellationToken cancellationToken);
}