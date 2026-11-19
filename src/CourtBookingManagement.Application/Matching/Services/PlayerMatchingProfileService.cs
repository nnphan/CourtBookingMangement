using CourtBookingManagement.Application.Matching.Models.Requests;
using CourtBookingManagement.Application.Matching.Models.Responses;
using CourtBookingManagement.Application.Matching.Repositories;
using CourtBookingManagement.Domain.Abstractions;
using FluentValidation;

namespace CourtBookingManagement.Application.Matching.Services;

public sealed class PlayerMatchingProfileService(
    IPlayerMatchingProfileRepository repository,
    IValidator<UpdatePlayerMatchingProfileRequest> profileValidator,
    IValidator<UpdatePlayerMatchPreferencesRequest> preferencesValidator) : IPlayerMatchingProfileService
{
    public async Task<Result<PlayerMatchingProfileResponse>> GetProfileAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty)
        {
            return Result.Failure<PlayerMatchingProfileResponse>(InvalidUser);
        }

        var profile = await repository.GetProfileAsync(userId, cancellationToken);
        return profile is null
            ? Result.Failure<PlayerMatchingProfileResponse>(ProfileRequired)
            : Result.Success(profile);
    }

    public async Task<Result<PlayerMatchingProfileResponse>> UpdateProfileAsync(
        UpdatePlayerMatchingProfileRequest request,
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return Result.Failure<PlayerMatchingProfileResponse>(
                new Error("PLAYER_MATCHING_PROFILE_INVALID", "Request cannot be null."));
        }

        if (userId == Guid.Empty)
        {
            return Result.Failure<PlayerMatchingProfileResponse>(InvalidUser);
        }

        var validation = await profileValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<PlayerMatchingProfileResponse>(
                new Error("PLAYER_MATCHING_PROFILE_INVALID", string.Join("; ", validation.Errors.Select(error => error.ErrorMessage))));
        }

        var profile = await repository.UpdateProfileAsync(userId, request, cancellationToken);
        return Result.Success(profile);
    }

    public async Task<Result<PlayerMatchPreferencesResponse>> GetPreferencesAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty)
        {
            return Result.Failure<PlayerMatchPreferencesResponse>(InvalidUser);
        }

        var preferences = await repository.GetPreferencesAsync(userId, cancellationToken);
        return preferences is null
            ? Result.Failure<PlayerMatchPreferencesResponse>(ProfileRequired)
            : Result.Success(preferences);
    }

    public async Task<Result<PlayerMatchPreferencesResponse>> UpdatePreferencesAsync(
        UpdatePlayerMatchPreferencesRequest request,
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return Result.Failure<PlayerMatchPreferencesResponse>(
                new Error("PREFERENCE_INVALID", "Request cannot be null."));
        }

        if (userId == Guid.Empty)
        {
            return Result.Failure<PlayerMatchPreferencesResponse>(InvalidUser);
        }

        var validation = await preferencesValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            var weightTotalIsInvalid = request.Preferences is not null
                && request.Preferences.SkillWeight + request.Preferences.TimeWeight
                    + request.Preferences.BranchWeight + request.Preferences.GenderWeight != 100;
            var errorCode = weightTotalIsInvalid
                ? "PREFERENCE_WEIGHT_TOTAL_INVALID"
                : "PREFERENCE_INVALID";

            return Result.Failure<PlayerMatchPreferencesResponse>(
                new Error(errorCode, string.Join("; ", validation.Errors.Select(error => error.ErrorMessage))));
        }

        if (!await repository.BranchesExistAsync(request.BranchIds, cancellationToken))
        {
            return Result.Failure<PlayerMatchPreferencesResponse>(
                new Error("PREFERENCE_BRANCH_INVALID", "One or more selected branches do not exist."));
        }

        if (!await repository.UpdatePreferencesAsync(userId, request, cancellationToken))
        {
            return Result.Failure<PlayerMatchPreferencesResponse>(ProfileRequired);
        }

        return Result.Success(await repository.GetPreferencesAsync(userId, cancellationToken)
            ?? throw new InvalidOperationException("Updated player preferences could not be read."));
    }

    private static readonly Error InvalidUser = new("PLAYER_MATCH.INVALID_USER", "A valid authenticated user is required.");
    private static readonly Error ProfileRequired = new("PLAYER_MATCHING_PROFILE_REQUIRED", "Create a player-matching profile before managing preferences.");
}
