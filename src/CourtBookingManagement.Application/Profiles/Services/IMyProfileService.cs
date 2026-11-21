using CourtBookingManagement.Application.Profiles.Models;
using CourtBookingManagement.Domain.Abstractions;

namespace CourtBookingManagement.Application.Profiles.Services;

public interface IMyProfileService
{
    Task<Result<MyProfileResponse>> GetMyProfileAsync(
        Guid userId,
        CancellationToken cancellationToken);

    Task<Result<MyProfileResponse>> UpdateMyProfileAsync(
        Guid userId,
        UpdateMyProfileRequest request,
        CancellationToken cancellationToken);
}
