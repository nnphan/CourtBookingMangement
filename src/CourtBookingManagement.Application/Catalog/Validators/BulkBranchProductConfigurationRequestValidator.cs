using CourtBookingManagement.Application.Catalog.Models.Requests;
using FluentValidation;

namespace CourtBookingManagement.Application.Catalog.Validators;

public sealed class BulkBranchProductConfigurationRequestValidator
    : AbstractValidator<BulkBranchProductConfigurationRequest>
{
    public BulkBranchProductConfigurationRequestValidator()
    {
        RuleFor(request => request.Items).NotEmpty().Must(items => items.Count <= 100);
        RuleForEach(request => request.Items).ChildRules(item =>
        {
            item.RuleFor(configuration => configuration.ProductId).NotEmpty();
            item.RuleFor(configuration => configuration.SellingPrice)
                .InclusiveBetween(0, 9_999_999_999.99m);
            item.RuleFor(configuration => configuration.CostPrice)
                .InclusiveBetween(0, 9_999_999_999.99m)
                .When(configuration => configuration.CostPrice.HasValue);
        });
        RuleFor(request => request.Items)
            .Must(items => items.Select(item => item.ProductId).Distinct().Count() == items.Count)
            .WithMessage("A product can appear only once in a bulk configuration.");
    }
}
