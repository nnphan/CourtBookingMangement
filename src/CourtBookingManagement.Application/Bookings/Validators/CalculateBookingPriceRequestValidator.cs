using CourtBookingManagement.Application.Bookings.DTOs;
using FluentValidation;

namespace CourtBookingManagement.Application.Bookings.Validators;

public sealed class CalculateBookingPriceRequestValidator : AbstractValidator<CalculateBookingPriceRequest>
{
    public CalculateBookingPriceRequestValidator()
    {
        RuleFor(request => request.BranchId)
            .NotEmpty()
            .WithMessage("BranchId is required.");
        RuleFor(request => request.BookingDate)
            .NotEmpty()
            .WithMessage("BookingDate is required.");
        RuleFor(request => request.StartTime)
            .NotEmpty()
            .WithMessage("StartTime is required.");
        RuleFor(request => request.EndTime)
            .NotEmpty()
            .WithMessage("EndTime is required.");
        RuleFor(request => request)
            .Must(request => request.EndTime > request.StartTime)
            .WithMessage("End time must be greater than start time.");
    }
}