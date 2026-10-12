using CourtBookingManagement.Application.CourtBookings.DTOs;
using CourtBookingManagement.Application.Common.Enums;
using FluentValidation;

namespace CourtBookingManagement.Application.CourtBookings.Validators;

public sealed class CreateCourtBookingRequestValidator : AbstractValidator<CreateCourtBookingRequest>
{
    public CreateCourtBookingRequestValidator()
    {
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.CourtId).NotEmpty();
        RuleFor(x => x.BookingType).Must(value => BookingTypes.All.Contains(value)).WithMessage("Invalid booking type.\n\nAllowed values:\nFixed, Flexible.");
        RuleFor(x => x.BookingDate).Must(date => date >= DateOnly.FromDateTime(DateTime.UtcNow)).WithMessage("Booking date must be today or a future date.");
        RuleFor(x => x.CustomerName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.PhoneNumber).Matches("^0[0-9]{9,10}$").WithMessage("Phone number must be a valid Vietnam phone number.");
        RuleFor(x => x.Note).MaximumLength(1000);
        RuleFor(x => x.PaymentMethod).Must(value => PaymentMethods.All.Contains(value)).WithMessage("Invalid payment method.\n\nAllowed values:\nCash, Momo, BankTransfer.");
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