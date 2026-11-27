using System.Net.Mail;
using System.Text;
using CourtBookingManagement.Application.StaffManagement.DTOs;
using FluentValidation;

namespace CourtBookingManagement.Application.StaffManagement.Validators;

public sealed class CreateStaffRequestValidator : AbstractValidator<CreateStaffRequest>
{
    public CreateStaffRequestValidator()
    {
        RuleFor(x => x.DisplayName)
            .Must(StaffValidationRules.DisplayName)
            .WithMessage("Display name must contain 2 to 100 characters.");
        RuleFor(x => x.Email)
            .Must(value => !string.IsNullOrWhiteSpace(value) &&
                value.Trim().Length <= 100 &&
                MailAddress.TryCreate(value.Trim(), out var parsed) &&
                string.Equals(parsed.Address, value.Trim(), StringComparison.OrdinalIgnoreCase))
            .WithMessage("A valid email address is required.");
        RuleFor(x => x.PhoneNumber)
            .Must(StaffValidationRules.PhoneNumber)
            .WithMessage("A phone number of at most 20 characters is required.");
        RuleFor(x => x.Password)
            .Must(value => !string.IsNullOrWhiteSpace(value) && value.Length >= 8 && Encoding.UTF8.GetByteCount(value) <= 72)
            .WithMessage("The initial password must contain at least 8 characters and at most 72 UTF-8 bytes.");
        RuleFor(x => x.Notes).MaximumLength(1000);
        RuleFor(x => x.BranchIds)
            .Must(StaffValidationRules.Branches)
            .WithMessage("At least one distinct branch is required.");
    }
}
