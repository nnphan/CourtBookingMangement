using CourtBookingManagement.Application.Common.Enums;
using CourtBookingManagement.Application.Sales.Models.Requests;
using FluentValidation;

namespace CourtBookingManagement.Application.Sales.Validators;

public sealed class CheckoutRequestValidator : AbstractValidator<CheckoutRequest>
{
    public CheckoutRequestValidator()
    {
        RuleFor(request => request.PaymentMethodCode)
            .NotEmpty()
            .Must(code => code is not null && PaymentMethods.All.Contains(code.Trim().ToUpperInvariant()))
            .WithMessage("PaymentMethodCode must be CASH, MOMO, or BANK_TRANSFER.");
        RuleFor(request => request.CustomerId).Must(id => id is null || id != Guid.Empty);
        RuleFor(request => request.BookingId).Must(id => id is null || id != Guid.Empty);
        RuleFor(request => request.Notes).MaximumLength(2000);
        RuleFor(request => request.Items)
            .NotEmpty()
            .Must(items => items is not null && items.Count <= 100);
        RuleForEach(request => request.Items).ChildRules(item =>
        {
            item.RuleFor(line => line.ProductId).NotEmpty();
            item.RuleFor(line => line.Quantity)
                .InclusiveBetween(0.001m, 999_999_999.999m)
                .Must(quantity => decimal.Round(quantity, 3) == quantity)
                .WithMessage("Quantity must be positive and support at most three decimal places.");
        });
        RuleFor(request => request.Items)
            .Must(items => items is not null &&
                items.Select(item => item.ProductId).Distinct().Count() == items.Count)
            .WithMessage("A product can appear only once in checkout.");
    }
}
