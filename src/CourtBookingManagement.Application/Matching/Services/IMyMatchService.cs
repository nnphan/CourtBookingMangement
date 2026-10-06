using CourtBookingManagement.Application.Matching.Models;
using CourtBookingManagement.Application.Matching.Models.Requests;
using CourtBookingManagement.Application.Matching.Models.Responses;
using CourtBookingManagement.Domain.Abstractions;

namespace CourtBookingManagement.Application.Matching.Services;

public interface IMyMatchService
{
    Task<Result<PagedResult<MyMatchResponse>>> GetMyMatchesAsync(
        MyMatchSearchRequest request,
        Guid currentUserId,
        CancellationToken cancellationToken);
}