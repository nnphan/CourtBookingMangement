using CourtBookingManagement.Application.Common.Constants;
using CourtBookingManagement.Application.Matching.Models.Requests;
using FluentValidation;

namespace CourtBookingManagement.Application.Matching.Validators;

public sealed class PlayerMatchSearchRequestValidator : AbstractValidator<PlayerMatchSearchRequest>
{
    public PlayerMatchSearchRequestValidator()
    {
        RuleFor(request => request.SkillLevel)
            .Must(skillLevel => SkillLevels.All.Contains(skillLevel, StringComparer.Ordinal))
            .When(request => !string.IsNullOrWhiteSpace(request.SkillLevel))
            .WithMessage("Invalid Skill Level.");

        RuleFor(request => request.GenderPreference)
            .Must(genderPreference => GenderPreferences.All.Contains(genderPreference, StringComparer.Ordinal))
            .When(request => !string.IsNullOrWhiteSpace(request.GenderPreference))
            .WithMessage("Invalid gender preference.");
    }
}