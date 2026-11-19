using CourtBookingManagement.Application.Matching.Models.Requests;
using FluentValidation;

namespace CourtBookingManagement.Application.Matching.Validators;

public sealed class PlayerMatchDraftRequestValidator : AbstractValidator<PlayerMatchDraftRequest>
{
    private static readonly string[] GameTypes = ["SINGLES", "DOUBLES"];
    private static readonly string[] GenderPolicies = ["ANY", "SPECIFIC"];
    private static readonly string[] Genders = ["MALE", "FEMALE", "OTHER"];

    public PlayerMatchDraftRequestValidator()
    {
        RuleFor(request => request.BranchId).NotEmpty();
        RuleFor(request => request.Title)
            .NotEmpty()
            .MinimumLength(5)
            .MaximumLength(200);
        RuleFor(request => request.GameType)
            .Must(value => GameTypes.Contains(value, StringComparer.Ordinal))
            .WithMessage("Game type must be SINGLES or DOUBLES.");
        RuleFor(request => request.MinimumRating)
            .NotNull()
            .WithMessage("Minimum rating is required.");
        RuleFor(request => request.MinimumRating)
            .InclusiveBetween(0, 1000)
            .When(request => request.MinimumRating.HasValue);
        RuleFor(request => request.MaximumRating)
            .NotNull()
            .WithMessage("Maximum rating is required.");
        RuleFor(request => request.MaximumRating)
            .InclusiveBetween(0, 1000)
            .When(request => request.MaximumRating.HasValue);
        RuleFor(request => request)
            .Must(request =>
                !request.MinimumRating.HasValue
                || !request.MaximumRating.HasValue
                || request.MinimumRating.Value <= request.MaximumRating.Value)
            .WithMessage("Minimum rating cannot exceed maximum rating.");
        RuleFor(request => request.RequiredPlayerCount).InclusiveBetween(2, 20);
        RuleFor(request => request.GenderPolicy)
            .Must(value => GenderPolicies.Contains(value, StringComparer.Ordinal))
            .WithMessage("Gender policy must be ANY or SPECIFIC.");
        RuleFor(request => request.RequiredGender)
            .Must(value => value is null || Genders.Contains(value, StringComparer.Ordinal))
            .WithMessage("Required gender must be MALE, FEMALE, or OTHER.");
        RuleFor(request => request)
            .Must(request => request.GenderPolicy != "ANY" || request.RequiredGender is null)
            .WithMessage("Required gender must be null when gender policy is ANY.");
        RuleFor(request => request)
            .Must(request => request.GenderPolicy != "SPECIFIC" || request.RequiredGender is not null)
            .WithMessage("A required gender must be selected for SPECIFIC policy.");
        RuleFor(request => request.ParticipationFee)
            .NotNull()
            .WithMessage("Participation fee is required.");
        RuleFor(request => request.ParticipationFee)
            .GreaterThanOrEqualTo(0m)
            .When(request => request.ParticipationFee.HasValue);
        RuleFor(request => request)
            .Must(request =>
                (request.MatchDate is null && request.StartTime is null && request.EndTime is null)
                || (request.MatchDate.HasValue && request.StartTime.HasValue && request.EndTime.HasValue))
            .WithMessage("Provide match date, start time, and end time together, or omit the full schedule.");
        RuleFor(request => request)
            .Must(request =>
                !request.StartTime.HasValue
                || !request.EndTime.HasValue
                || request.StartTime.Value < request.EndTime.Value)
            .WithMessage("Start time must be earlier than end time.");
    }
}
