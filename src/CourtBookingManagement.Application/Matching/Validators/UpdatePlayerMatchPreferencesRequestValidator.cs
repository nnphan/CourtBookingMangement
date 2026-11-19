using FluentValidation;
using CourtBookingManagement.Application.Matching.Models;
using CourtBookingManagement.Application.Matching.Models.Requests;

namespace CourtBookingManagement.Application.Matching.Validators;

public sealed class UpdatePlayerMatchPreferencesRequestValidator : AbstractValidator<UpdatePlayerMatchPreferencesRequest>
{
    public UpdatePlayerMatchPreferencesRequestValidator()
    {
        RuleFor(request => request.Preferences)
            .Cascade(CascadeMode.Stop)
            .NotNull()
            .SetValidator(new PlayerMatchPreferenceWeightsValidator());

        RuleFor(request => request.BranchIds)
            .Cascade(CascadeMode.Stop)
            .NotNull()
            .NotEmpty()
            .Must(branchIds => branchIds.Distinct().Count() == branchIds.Count)
            .WithMessage("Branch IDs must be non-empty and distinct.");
        RuleForEach(request => request.BranchIds).NotEmpty();

        RuleFor(request => request.MinimumTimeOverlapMinutes).GreaterThan(0);
        RuleFor(request => request.MaximumParticipationFee)
            .GreaterThanOrEqualTo(0)
            .When(request => request.MaximumParticipationFee.HasValue);

        RuleFor(request => request)
            .Must(request =>
                request.PreferredStartTime.HasValue == request.PreferredEndTime.HasValue
                && (!request.PreferredStartTime.HasValue
                    || (request.PreferredStartTime < request.PreferredEndTime
                        && request.MinimumTimeOverlapMinutes
                            <= (request.PreferredEndTime.Value - request.PreferredStartTime.Value).TotalMinutes)))
            .WithMessage("Preferred times must be provided together, start before end, and cover the minimum overlap.");
    }
}

public sealed class PlayerMatchPreferenceWeightsValidator : AbstractValidator<PlayerMatchPreferenceWeights>
{
    public PlayerMatchPreferenceWeightsValidator()
    {
        RuleFor(preferences => preferences.SkillWeight).InclusiveBetween(0, 100);
        RuleFor(preferences => preferences.TimeWeight).InclusiveBetween(0, 100);
        RuleFor(preferences => preferences.BranchWeight).InclusiveBetween(0, 100);
        RuleFor(preferences => preferences.GenderWeight).InclusiveBetween(0, 100);
        RuleFor(preferences => preferences)
            .Must(preferences => preferences.SkillWeight + preferences.TimeWeight
                + preferences.BranchWeight + preferences.GenderWeight == 100)
            .WithMessage("Preference weights must sum exactly to 100.");
    }
}
