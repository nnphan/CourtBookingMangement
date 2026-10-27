using CourtBookingManagement.Application.Owners.Models;
using FluentValidation;

namespace CourtBookingManagement.Application.Owners.Validators;

public sealed class CreateOwnerRequestValidator : AbstractValidator<CreateOwnerRequest>
{
    public CreateOwnerRequestValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.BusinessName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.TaxId).MaximumLength(50);
    }
}

public sealed class UpdateOwnerRequestValidator : AbstractValidator<UpdateOwnerRequest>
{
    public UpdateOwnerRequestValidator()
    {
        RuleFor(x => x.BusinessName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.TaxId).MaximumLength(50);
    }
}
