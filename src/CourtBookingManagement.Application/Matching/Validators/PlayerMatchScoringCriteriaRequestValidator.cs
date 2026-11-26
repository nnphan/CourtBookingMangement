using CourtBookingManagement.Application.Matching.Models.Requests;
using FluentValidation;

namespace CourtBookingManagement.Application.Matching.Validators;

public sealed class PlayerMatchScoringCriteriaRequestValidator : AbstractValidator<PlayerMatchScoringCriteriaRequest>
{
    private static readonly string[] GameTypes = ["SINGLES", "DOUBLES"];

    public PlayerMatchScoringCriteriaRequestValidator()
    {
        RuleFor(request => request.BranchIds)
            .Cascade(CascadeMode.Stop)
            .NotNull()
            .Must(branchIds => branchIds.All(branchId => branchId != Guid.Empty))
            .Must(branchIds => branchIds.Distinct().Count() == branchIds.Count)
            .WithMessage("Branch IDs must be non-empty, valid, and distinct.");
        RuleFor(request => request.Keyword).MaximumLength(150);
        RuleFor(request => request.SkillLevel)
            .Must(value => value is null or "BEGINNER" or "INTERMEDIATE" or "ADVANCED" or "PROFESSIONAL");
        RuleFor(request => request.GenderPreference)
            .Must(value => value is null or "MALE" or "FEMALE" or "BOTH");
        RuleFor(request => request)
            .Must(request => request.StartTime.HasValue == request.EndTime.HasValue
                && (!request.StartTime.HasValue || request.StartTime < request.EndTime))
            .WithMessage("Start and end must be supplied together, with start earlier than end.");
        RuleFor(request => request)
            .Must(request => request.PreferredStartTime.HasValue == request.PreferredEndTime.HasValue)
            .WithMessage("Preferred start and end times must be supplied together.");
        RuleFor(request => request.MinimumTimeOverlapMinutes)
            .GreaterThan(0);
        RuleFor(request => request)
            .Must(request =>
                !request.PreferredStartTime.HasValue
                || !request.PreferredEndTime.HasValue
                || request.PreferredStartTime.Value < request.PreferredEndTime.Value)
            .WithMessage("Preferred start time must be earlier than preferred end time.");
        RuleFor(request => request)
            .Must(request =>
                !request.PreferredStartTime.HasValue
                || !request.PreferredEndTime.HasValue
                || request.MinimumTimeOverlapMinutes <=
                    (request.PreferredEndTime.Value - request.PreferredStartTime.Value).TotalMinutes)
            .WithMessage("Minimum overlap cannot exceed the preferred time interval.");
        RuleFor(request => request.GameTypes)
            .Cascade(CascadeMode.Stop)
            .NotNull()
            .Must(gameTypes => gameTypes.All(gameType => GameTypes.Contains(gameType, StringComparer.Ordinal)))
            .Must(gameTypes => gameTypes.Distinct(StringComparer.Ordinal).Count() == gameTypes.Count)
            .WithMessage("Game types must be distinct SINGLES or DOUBLES values.");
        RuleFor(request => request.MaximumParticipationFee)
            .GreaterThanOrEqualTo(0m)
            .When(request => request.MaximumParticipationFee.HasValue);
        RuleFor(request => request.Preferences)
            .Must(preferences => preferences is null
                || new[]
                {
                    preferences.SkillWeight,
                    preferences.TimeWeight,
                    preferences.BranchWeight,
                    preferences.GenderWeight
                }.All(weight => weight is >= 0m and <= 100m))
            .WithMessage("Each preference weight must be between 0 and 100.");
    }
}
