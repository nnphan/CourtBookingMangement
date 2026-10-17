using CourtBookingManagement.Application.Auth.DTOs;
using FluentValidation;

namespace CourtBookingManagement.Application.Auth.Validators;

public sealed class LoginByPhoneNumberRequestValidator : AbstractValidator<LoginByPhoneNumberRequest>
{
    public LoginByPhoneNumberRequestValidator()
    {
        RuleFor(request => request.PhoneNumber)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Must(phoneNumber => !string.IsNullOrWhiteSpace(phoneNumber))
            .WithMessage("Phone number is required.")
            .MaximumLength(20)
            .WithMessage("Phone number cannot exceed 20 characters.");

        RuleFor(request => request.Password)
            .NotEmpty()
            .WithMessage("Password is required.");
    }
}