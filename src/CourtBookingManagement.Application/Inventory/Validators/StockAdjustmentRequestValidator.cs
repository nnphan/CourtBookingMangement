using CourtBookingManagement.Application.Inventory.Models.Requests;
using FluentValidation;

namespace CourtBookingManagement.Application.Inventory.Validators;

public sealed class StockAdjustmentRequestValidator : AbstractValidator<StockAdjustmentRequest>
{
    public StockAdjustmentRequestValidator()
    {
        RuleFor(request => request.ProductId).NotEmpty();
        RuleFor(request => request.MovementType)
            .Must(type => type is "ADJUSTMENT_IN" or "ADJUSTMENT_OUT" or "DAMAGE")
            .WithMessage("MovementType must be ADJUSTMENT_IN, ADJUSTMENT_OUT, or DAMAGE.");
        RuleFor(request => request.Quantity)
            .InclusiveBetween(0.001m, 999_999_999.999m)
            .Must(quantity => decimal.Round(quantity, 3) == quantity)
            .WithMessage("Quantity supports at most three decimal places.");
        RuleFor(request => request.Notes)
            .NotEmpty()
            .Must(notes => !string.IsNullOrWhiteSpace(notes))
            .MaximumLength(2000);
    }
}
