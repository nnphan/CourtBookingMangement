using System.Data;
using CourtBookingManagement.Application.Abstractions.Data;
using CourtBookingManagement.Application.Files;
using CourtBookingManagement.Application.Profiles.Models;
using CourtBookingManagement.Application.Profiles.Repositories;
using CourtBookingManagement.Domain.Abstractions;
using FluentValidation;

namespace CourtBookingManagement.Application.Profiles.Services;

public sealed class MyProfileService(
    IMyProfileRepository repository,
    IAvatarFilePolicy avatarFilePolicy,
    ISqlConnectionFactory sqlConnectionFactory,
    IValidator<UpdateMyProfileRequest> updateValidator) : IMyProfileService
{
    private static readonly Error ProfileNotFound =
        new("PROFILE_NOT_FOUND", "The current user's profile was not found.");

    public async Task<Result<MyProfileResponse>> GetMyProfileAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var profile = await repository.GetByUserIdAsync(userId, null, cancellationToken);
        return profile is null
            ? Result.Failure<MyProfileResponse>(ProfileNotFound)
            : Result.Success(ToResponse(profile));
    }

    public async Task<Result<MyProfileResponse>> UpdateMyProfileAsync(
        Guid userId,
        UpdateMyProfileRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validation = await updateValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            throw new FluentValidation.ValidationException(validation.Errors);
        }

        using var connection = sqlConnectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);

        var currentProfile = await repository.GetByUserIdAsync(
            userId,
            transaction,
            cancellationToken);
        if (currentProfile is null)
        {
            return Result.Failure<MyProfileResponse>(ProfileNotFound);
        }

        var newAvatarFileId = request.RemoveAvatar
            ? null
            : request.AvatarFileId ?? currentProfile.AvatarFileId;

        if (request.AvatarFileId is Guid requestedAvatarFileId)
        {
            var avatarValidation = await avatarFilePolicy.ValidateForUserAsync(
                requestedAvatarFileId,
                userId,
                currentProfile.AvatarFileId,
                transaction,
                cancellationToken);
            if (avatarValidation.IsFailure)
            {
                return Result.Failure<MyProfileResponse>(avatarValidation.Error);
            }
        }

        await repository.UpdateAsync(
            userId,
            request.DisplayName.Trim(),
            currentProfile.AvatarFileId,
            newAvatarFileId,
            transaction,
            cancellationToken);

        var updatedProfile = await repository.GetByUserIdAsync(
            userId,
            transaction,
            cancellationToken);
        if (updatedProfile is null)
        {
            throw new InvalidOperationException(
                "The profile disappeared during a successful profile update.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        transaction.Commit();
        return Result.Success(ToResponse(updatedProfile));
    }

    private static MyProfileResponse ToResponse(MyProfileRecord profile) =>
        new()
        {
            UserId = profile.UserId,
            CustomerId = profile.CustomerId,
            DisplayName = profile.DisplayName,
            Email = profile.Email,
            PhoneNumber = profile.PhoneNumber,
            Avatar = profile.AvatarFileId is Guid fileId
                && profile.AvatarUrl is not null
                && profile.AvatarFileName is not null
                    ? new AvatarFileResponse
                    {
                        FileId = fileId,
                        Url = profile.AvatarUrl,
                        FileName = profile.AvatarFileName
                    }
                    : null,
            UpdatedAt = profile.UpdatedAt
        };
}
