using CourtBookingManagement.Application.Sales.Models.Requests;
using FluentValidation;

namespace CourtBookingManagement.Application.Sales.Validators;

public sealed class SalesReportRequestValidator : AbstractValidator<SalesReportRequest>
{
    public SalesReportRequestValidator()
    {
        RuleFor(request => request.From).NotEmpty();
        RuleFor(request => request.To).NotEmpty();
        RuleFor(request => request)
            .Must(request =>
                request.To >= request.From &&
                request.To.DayNumber - request.From.DayNumber <= 365)
            .WithMessage("The report date range must be ordered and no longer than 366 days.");
    }
}
