using FluentValidation;

namespace CourtBookingManagement.Application.Matching.Validators;

public sealed class UpdatePlayerMatchingProfileRequestValidator : AbstractValidator<Models.Requests.UpdatePlayerMatchingProfileRequest>
{
    public UpdatePlayerMatchingProfileRequestValidator()
    {
        RuleFor(request => request.CurrentRating)
            .InclusiveBetween(0, 1000);

        RuleFor(request => request.Gender)
            .Must(gender => gender is null or "MALE" or "FEMALE" or "OTHER")
            .WithMessage("Gender must be MALE, FEMALE, OTHER, or null.");
    }
}
