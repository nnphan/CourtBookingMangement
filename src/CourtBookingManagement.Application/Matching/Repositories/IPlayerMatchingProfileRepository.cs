using CourtBookingManagement.Application.Matching.Models.Requests;
using CourtBookingManagement.Application.Matching.Models.Responses;

namespace CourtBookingManagement.Application.Matching.Repositories;

public interface IPlayerMatchingProfileRepository
{
    Task<PlayerMatchingProfileResponse?> GetProfileAsync(Guid userId, CancellationToken cancellationToken);

    Task<PlayerMatchingProfileResponse> UpdateProfileAsync(
        Guid userId,
        UpdatePlayerMatchingProfileRequest request,
        CancellationToken cancellationToken);

    Task<PlayerMatchPreferencesResponse?> GetPreferencesAsync(Guid userId, CancellationToken cancellationToken);

    Task<bool> BranchesExistAsync(IReadOnlyList<Guid> branchIds, CancellationToken cancellationToken);

    Task<bool> UpdatePreferencesAsync(
        Guid userId,
        UpdatePlayerMatchPreferencesRequest request,
        CancellationToken cancellationToken);
}
