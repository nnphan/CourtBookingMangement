using CourtBookingManagement.Application.Catalog.Models.Requests;
using FluentValidation;

namespace CourtBookingManagement.Application.Catalog.Validators;

public sealed class BranchProductConfigurationRequestValidator
    : AbstractValidator<BranchProductConfigurationRequest>
{
    private const decimal MaximumPrice = 9_999_999_999.99m;

    public BranchProductConfigurationRequestValidator()
    {
        RuleFor(request => request.SellingPrice).InclusiveBetween(0, MaximumPrice);
        RuleFor(request => request.CostPrice)
            .InclusiveBetween(0, MaximumPrice)
            .When(request => request.CostPrice.HasValue);
    }
}
