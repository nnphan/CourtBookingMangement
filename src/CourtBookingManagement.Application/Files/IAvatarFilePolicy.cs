using System.Data;
using CourtBookingManagement.Domain.Abstractions;

namespace CourtBookingManagement.Application.Files;

public interface IAvatarFilePolicy
{
    Task<Result<ValidatedAvatarFile>> ValidateForUserAsync(
        Guid fileId,
        Guid userId,
        Guid? currentAvatarFileId,
        IDbTransaction transaction,
        CancellationToken cancellationToken);
}
