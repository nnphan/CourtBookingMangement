using CourtBookingManagement.Application.Catalog.Models.Requests;
using FluentValidation;

namespace CourtBookingManagement.Application.Catalog.Validators;

public sealed class CreateProductCategoryRequestValidator : AbstractValidator<CreateProductCategoryRequest>
{
    public CreateProductCategoryRequestValidator()
    {
        RuleFor(request => request.Code).NotEmpty().MaximumLength(50);
        RuleFor(request => request.Name).NotEmpty().MaximumLength(150);
    }
}
