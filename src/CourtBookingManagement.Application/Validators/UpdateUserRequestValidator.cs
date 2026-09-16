using CourtBookingManagement.Application.Users.DTOs;
using FluentValidation;

namespace CourtBookingManagement.Application.Validators;

public sealed class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequestDto>
{
    public UpdateUserRequestValidator()
    {
        RuleFor(request => request.Email)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("Email is required")
            .MaximumLength(100)
            .WithMessage("Email must not exceed 100 characters")
            .EmailAddress()
            .WithMessage("Email must be a valid email address");

        RuleFor(request => request.PasswordHash)
            .MaximumLength(500)
            .WithMessage("Password hash must not exceed 500 characters")
            .When(request => request.PasswordHash is not null);

        RuleFor(request => request.PhoneNumber)
            .MaximumLength(20)
            .WithMessage("Phone number must not exceed 20 characters")
            .When(request => request.PhoneNumber is not null);

        RuleFor(request => request.UpdatedBy)
            .NotEqual(Guid.Empty)
            .WithMessage("UpdatedBy cannot be an empty GUID")
            .When(request => request.UpdatedBy.HasValue);
    }
}