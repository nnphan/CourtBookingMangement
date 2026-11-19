using CourtBookingManagement.Application.Matching.Models.Requests;
using CourtBookingManagement.Application.Matching.Models.Responses;
using CourtBookingManagement.Domain.Abstractions;

namespace CourtBookingManagement.Application.Matching.Services;

public interface IPlayerMatchingProfileService
{
    Task<Result<PlayerMatchingProfileResponse>> GetProfileAsync(Guid userId, CancellationToken cancellationToken);

    Task<Result<PlayerMatchingProfileResponse>> UpdateProfileAsync(
        UpdatePlayerMatchingProfileRequest request,
        Guid userId,
        CancellationToken cancellationToken);

    Task<Result<PlayerMatchPreferencesResponse>> GetPreferencesAsync(Guid userId, CancellationToken cancellationToken);

    Task<Result<PlayerMatchPreferencesResponse>> UpdatePreferencesAsync(
        UpdatePlayerMatchPreferencesRequest request,
        Guid userId,
        CancellationToken cancellationToken);
}
