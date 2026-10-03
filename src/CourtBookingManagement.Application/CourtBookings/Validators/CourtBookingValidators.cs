using CourtBookingManagement.Application.CourtBookings.DTOs;
using FluentValidation;
using System.Text.RegularExpressions;

namespace CourtBookingManagement.Application.CourtBookings.Validators;

public sealed class CreateCourtBookingRequestValidator : AbstractValidator<CreateCourtBookingRequest>
{
    public CreateCourtBookingRequestValidator()
    {
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.CourtId).NotEmpty();
        RuleFor(x => x.BookingDate).Must(date => date >= DateOnly.FromDateTime(DateTime.UtcNow)).WithMessage("Booking date must be today or a future date.");
        RuleFor(x => x.CustomerName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.PhoneNumber).Matches("^0[0-9]{9,10}$").WithMessage("Phone number must be a valid Vietnam phone number.");
        RuleFor(x => x.Note).MaximumLength(1000);
        RuleFor(x => x.PaymentMethod).Must(value => value.Equals("Cash", StringComparison.OrdinalIgnoreCase) || value.Equals("BankTransfer", StringComparison.OrdinalIgnoreCase) || value.Equals("MoMo", StringComparison.OrdinalIgnoreCase)).WithMessage("Payment method is invalid.");
        RuleFor(x => x).Must(x => x.StartTime < x.EndTime).WithMessage("Start time must be before end time.");
    }
}

public sealed class CheckCourtAvailabilityRequestValidator : AbstractValidator<CheckCourtAvailabilityRequest>
{
    public CheckCourtAvailabilityRequestValidator()
    {
        RuleFor(x => x.CourtId).NotEmpty();
        RuleFor(x => x.BookingDate).Must(date => date >= DateOnly.FromDateTime(DateTime.UtcNow)).WithMessage("Booking date must be today or a future date.");
        RuleFor(x => x).Must(x => x.StartTime < x.EndTime).WithMessage("Start time must be before end time.");
    }
}

public sealed class SearchCourtBookingsRequestValidator : AbstractValidator<SearchCourtBookingsRequest>
{
    public SearchCourtBookingsRequestValidator() => RuleFor(x => x.PhoneNumber).Matches("^0[0-9]{9,10}$").WithMessage("Phone number must be a valid Vietnam phone number.");
}

public sealed class CancelCourtBookingRequestValidator : AbstractValidator<CancelCourtBookingRequest>
{
    public CancelCourtBookingRequestValidator()
    {
        RuleFor(x => x.BookingCode).NotEmpty().MaximumLength(20);
        RuleFor(x => x.PhoneNumber).Matches("^0[0-9]{9,10}$").WithMessage("Phone number must be a valid Vietnam phone number.");
    }
}