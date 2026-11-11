using CourtBookingManagement.Application.Sales.Models.Requests;
using FluentValidation;

namespace CourtBookingManagement.Application.Sales.Validators;

public sealed class CancelSalesOrderRequestValidator : AbstractValidator<CancelSalesOrderRequest>
{
    public CancelSalesOrderRequestValidator()
    {
        RuleFor(request => request.Reason)
            .NotEmpty()
            .Must(reason => !string.IsNullOrWhiteSpace(reason))
            .MaximumLength(1000);
    }
}
