using System.Data;
using CourtBookingManagement.Domain.Abstractions;

namespace CourtBookingManagement.Application.Files;

public sealed class AvatarFilePolicy(IFileMetadataRepository fileMetadataRepository)
    : IAvatarFilePolicy
{
    private const long MaximumAvatarSize = 5 * 1024 * 1024;
    private static readonly HashSet<string> AllowedContentTypes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "image/jpeg",
            "image/jpg",
            "image/png",
            "image/webp"
        };

    public async Task<Result<ValidatedAvatarFile>> ValidateForUserAsync(
        Guid fileId,
        Guid userId,
        Guid? currentAvatarFileId,
        IDbTransaction transaction,
        CancellationToken cancellationToken)
    {
        var file = await fileMetadataRepository.GetByIdAsync(fileId, transaction, cancellationToken);
        if (file is null)
        {
            return Failure("AVATAR_FILE_NOT_FOUND", "Avatar file was not found.");
        }

        if (file.UploaderUserId != userId)
        {
            return Failure("AVATAR_FILE_NOT_OWNED", "Avatar file does not belong to the current user.");
        }

        if (file.IsDeleted)
        {
            return Failure("AVATAR_FILE_NOT_ATTACHABLE", "Avatar file is no longer available.");
        }

        if (!string.Equals(file.Category, "avatar", StringComparison.OrdinalIgnoreCase)
            || !AllowedContentTypes.Contains(file.ContentType))
        {
            return Failure("AVATAR_FILE_INVALID_TYPE", "Avatar file must be a supported image.");
        }

        if (file.SizeBytes <= 0 || file.SizeBytes > MaximumAvatarSize)
        {
            return Failure("AVATAR_FILE_TOO_LARGE", "Avatar file exceeds the 5 MB limit.");
        }

        var isTemporary = string.Equals(file.Status, "TEMPORARY", StringComparison.Ordinal);
        var isCurrentAttachedFile = currentAvatarFileId == file.Id
            && string.Equals(file.Status, "ATTACHED", StringComparison.Ordinal);
        if (!isTemporary && !isCurrentAttachedFile)
        {
            return Failure("AVATAR_FILE_NOT_ATTACHABLE", "Avatar file is not eligible for attachment.");
        }

        return Result.Success(new ValidatedAvatarFile(
            file.Id,
            file.SecureUrl,
            file.FileName,
            file.ContentType,
            file.SizeBytes));
    }

    private static Result<ValidatedAvatarFile> Failure(string code, string description) =>
        Result.Failure<ValidatedAvatarFile>(new Error(code, description));
}
