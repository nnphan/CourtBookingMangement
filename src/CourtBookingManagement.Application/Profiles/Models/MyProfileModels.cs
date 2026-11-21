namespace CourtBookingManagement.Application.Profiles.Models;

public sealed record MyProfileRecord(
    Guid UserId,
    Guid? CustomerId,
    string? DisplayName,
    string Email,
    string? PhoneNumber,
    Guid? AvatarFileId,
    string? AvatarUrl,
    string? AvatarFileName,
    DateTime UpdatedAt);

public sealed class MyProfileResponse
{
    public Guid UserId { get; init; }

    public Guid? CustomerId { get; init; }

    public string? DisplayName { get; init; }

    public string Email { get; init; } = string.Empty;

    public string? PhoneNumber { get; init; }

    public AvatarFileResponse? Avatar { get; init; }

    public DateTime UpdatedAt { get; init; }
}

public sealed class AvatarFileResponse
{
    public Guid FileId { get; init; }

    public string Url { get; init; } = string.Empty;

    public string FileName { get; init; } = string.Empty;
}

public sealed class UpdateMyProfileRequest
{
    public string DisplayName { get; init; } = string.Empty;

    public Guid? AvatarFileId { get; init; }

    public bool RemoveAvatar { get; init; }
}
