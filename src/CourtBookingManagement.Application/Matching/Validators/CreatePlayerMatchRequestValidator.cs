using CourtBookingManagement.Application.Matching.Models.Requests;
using FluentValidation;

namespace CourtBookingManagement.Application.Matching.Validators;

public sealed class CreatePlayerMatchRequestValidator : AbstractValidator<CreatePlayerMatchRequest>
{
    public CreatePlayerMatchRequestValidator()
    {
        RuleFor(x => x.BranchId).NotEmpty();

        RuleFor(x => x.Title)
            .NotEmpty()
            .MinimumLength(5).WithMessage("Title must be at least 5 characters.")
            .MaximumLength(200).WithMessage("Title must not exceed 200 characters.");

        RuleFor(x => x.MatchDate)
            .Must(date => date >= DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("Match date must not be in the past.");

        RuleFor(x => x.StartTime)
            .NotEmpty();

        RuleFor(x => x.EndTime)
            .NotEmpty();

        RuleFor(x => x)
            .Must(x => x.StartTime < x.EndTime)
            .WithMessage("Start time must be earlier than end time.");

        RuleFor(x => x)
            .Must(x =>
            {
                var duration = x.EndTime.ToTimeSpan() - x.StartTime.ToTimeSpan();
                return duration >= TimeSpan.FromHours(1) && duration <= TimeSpan.FromHours(6);
            })
            .WithMessage("Match duration must be between 1 and 6 hours.");

        RuleFor(x => x.MaxPlayers)
            .InclusiveBetween(2, 20)
            .WithMessage("Max players must be between 2 and 20.");

        RuleFor(x => x.FeePerPlayer)
            .GreaterThanOrEqualTo(0m)
            .When(x => x.FeePerPlayer.HasValue)
            .WithMessage("Fee per player cannot be negative.");
    }
}
