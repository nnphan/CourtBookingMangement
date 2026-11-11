using CourtBookingManagement.Application.Inventory.Models.Requests;
using FluentValidation;

namespace CourtBookingManagement.Application.Inventory.Validators;

public sealed class GoodsReceiptDraftRequestValidator : AbstractValidator<GoodsReceiptDraftRequest>
{
    public GoodsReceiptDraftRequestValidator()
    {
        RuleFor(request => request.Items)
            .NotEmpty()
            .Must(items => items is not null && items.Count <= 500);
        RuleFor(request => request.SupplierName).MaximumLength(200);
        RuleFor(request => request.SupplierReference).MaximumLength(100);
        RuleFor(request => request.Notes).MaximumLength(2000);
        RuleForEach(request => request.Items).ChildRules(item =>
        {
            item.RuleFor(receiptItem => receiptItem.ProductId).NotEmpty();
            item.RuleFor(receiptItem => receiptItem.Quantity)
                .InclusiveBetween(0.001m, 999_999_999.999m);
            item.RuleFor(receiptItem => receiptItem.UnitCost)
                .InclusiveBetween(0, 9_999_999_999.99m);
        });
        RuleFor(request => request.Items)
            .Must(items => items is not null &&
                items.Select(item => item.ProductId).Distinct().Count() == items.Count)
            .WithMessage("A product can appear only once in a goods receipt.");
        RuleFor(request => request.Items)
            .Must(items => items is not null && items.All(item =>
                    item.Quantity is > 0 and <= 999_999_999.999m &&
                    item.UnitCost is >= 0 and <= 9_999_999_999.99m) &&
                items.Sum(item => decimal.Round(
                    item.Quantity * item.UnitCost,
                    2,
                    MidpointRounding.AwayFromZero)) <= 9_999_999_999.99m)
            .WithMessage("The goods receipt total exceeds the supported amount.");
        RuleForEach(request => request.Items).ChildRules(item =>
        {
            item.RuleFor(receiptItem => receiptItem.Quantity)
                .Must(quantity => decimal.Round(quantity, 3) == quantity)
                .WithMessage("Quantity supports at most three decimal places.");
            item.RuleFor(receiptItem => receiptItem.UnitCost)
                .Must(cost => decimal.Round(cost, 2) == cost)
                .WithMessage("UnitCost supports at most two decimal places.");
        });
    }
}
