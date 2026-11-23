using CourtBookingManagement.Application.StaffManagement.DTOs;
using FluentValidation;

namespace CourtBookingManagement.Application.StaffManagement.Validators;

public sealed class ChangeStaffStatusRequestValidator : AbstractValidator<ChangeStaffStatusRequest>
{
    public ChangeStaffStatusRequestValidator()
    {
        RuleFor(x => x.Reason).MaximumLength(500);
    }
}
