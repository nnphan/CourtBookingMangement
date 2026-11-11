using CourtBookingManagement.Application.Catalog.Models.Requests;
using FluentValidation;

namespace CourtBookingManagement.Application.Catalog.Validators;

public sealed class UpdateProductRequestValidator : AbstractValidator<UpdateProductRequest>
{
    private const decimal MaximumPrice = 9_999_999_999.99m;

    public UpdateProductRequestValidator()
    {
        RuleFor(request => request.CategoryId).NotEmpty();
        RuleFor(request => request.Sku).NotEmpty().MaximumLength(100);
        RuleFor(request => request.Barcode).MaximumLength(100);
        RuleFor(request => request.Name).NotEmpty().MaximumLength(200);
        RuleFor(request => request.Unit).NotEmpty().MaximumLength(50);
        RuleFor(request => request.DefaultCostPrice)
            .InclusiveBetween(0, MaximumPrice)
            .When(request => request.DefaultCostPrice.HasValue);
        RuleFor(request => request.DefaultSellingPrice).InclusiveBetween(0, MaximumPrice);
    }
}
