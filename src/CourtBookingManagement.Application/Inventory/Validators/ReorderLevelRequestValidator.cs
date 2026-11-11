using CourtBookingManagement.Application.Inventory.Models.Requests;
using FluentValidation;

namespace CourtBookingManagement.Application.Inventory.Validators;

public sealed class ReorderLevelRequestValidator : AbstractValidator<ReorderLevelRequest>
{
    public ReorderLevelRequestValidator()
    {
        RuleFor(request => request.ReorderLevel)
            .InclusiveBetween(0, 999_999_999.999m)
            .Must(level => decimal.Round(level, 3) == level)
            .WithMessage("ReorderLevel must be non-negative and support at most three decimal places.");
    }
}
