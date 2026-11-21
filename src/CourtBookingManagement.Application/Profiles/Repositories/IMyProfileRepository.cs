using System.Data;
using CourtBookingManagement.Application.Profiles.Models;

namespace CourtBookingManagement.Application.Profiles.Repositories;

public interface IMyProfileRepository
{
    Task<MyProfileRecord?> GetByUserIdAsync(
        Guid userId,
        IDbTransaction? transaction,
        CancellationToken cancellationToken);

    Task UpdateAsync(
        Guid userId,
        string displayName,
        Guid? previousAvatarFileId,
        Guid? newAvatarFileId,
        IDbTransaction transaction,
        CancellationToken cancellationToken);
}
