using CourtBookingManagement.Application.Profiles.Models;
using FluentValidation;

namespace CourtBookingManagement.Application.Profiles.Validators;

public sealed class UpdateMyProfileRequestValidator : AbstractValidator<UpdateMyProfileRequest>
{
    public UpdateMyProfileRequestValidator()
    {
        RuleFor(request => request.DisplayName)
            .Must(value => !string.IsNullOrWhiteSpace(value)
                && value.Trim().Length is >= 2 and <= 150)
            .WithMessage("Display name must be between 2 and 150 characters.")
            .OverridePropertyName("displayName");

        RuleFor(request => request.AvatarFileId)
            .Must(fileId => fileId is null || fileId != Guid.Empty)
            .WithMessage("A valid avatar file ID is required.")
            .OverridePropertyName("avatarFileId");

        RuleFor(request => request)
            .Must(value => !value.RemoveAvatar || value.AvatarFileId is null)
            .WithMessage("Avatar file ID cannot be supplied when removing the avatar.")
            .OverridePropertyName("avatarFileId");
    }
}
