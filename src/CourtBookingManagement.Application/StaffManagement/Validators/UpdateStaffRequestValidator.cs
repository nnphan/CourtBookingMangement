using CourtBookingManagement.Application.StaffManagement.DTOs;
using FluentValidation;

namespace CourtBookingManagement.Application.StaffManagement.Validators;

public sealed class UpdateStaffRequestValidator : AbstractValidator<UpdateStaffRequest>
{
    public UpdateStaffRequestValidator()
    {
        RuleFor(x => x.DisplayName)
            .Must(StaffValidationRules.DisplayName)
            .WithMessage("Display name must contain 2 to 100 characters.");
        RuleFor(x => x.PhoneNumber)
            .Must(StaffValidationRules.PhoneNumber)
            .WithMessage("A phone number of at most 20 characters is required.");
        RuleFor(x => x.StaffCode)
            .Must(StaffValidationRules.StaffCode);
        RuleFor(x => x.JobTitle)
            .Must(StaffValidationRules.JobTitle);
        RuleFor(x => x.Notes).MaximumLength(1000);
        RuleFor(x => x.BranchIds)
            .Must(StaffValidationRules.Branches)
            .WithMessage("At least one distinct branch is required.");
    }
}
