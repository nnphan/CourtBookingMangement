using FluentValidation;
using FluentValidation.Results;

namespace CourtBookingManagement.Infrastructure.FileStorage;

public sealed class MediaFolderResolver : IMediaFolderResolver
{
    public string Resolve(string category)
    {
        var normalizedCategory = category?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedCategory)
            || !Enum.TryParse<MediaCategory>(normalizedCategory, true, out var mediaCategory)
            || !Enum.IsDefined(mediaCategory)
            || !string.Equals(mediaCategory.ToString(), normalizedCategory, StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException(
                [new ValidationFailure("category", "Unsupported media category")]);
        }

        return mediaCategory switch
        {
            MediaCategory.Branch => FileStorageFolders.Branches,
            MediaCategory.Court => "badminton-booking/courts",
            MediaCategory.Avatar => "badminton-booking/avatars",
            MediaCategory.Tournament => "badminton-booking/tournaments",
            MediaCategory.Promotion => "badminton-booking/promotions",
            _ => throw new ValidationException(
                [new ValidationFailure("category", "Unsupported media category")])
        };
    }
}