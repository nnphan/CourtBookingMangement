using CourtBookingManagement.Application.Bookings.DTOs;
using CourtBookingManagement.Application.Common.Enums;
using CourtBookingManagement.Application.Options;
using FluentValidation;

namespace CourtBookingManagement.Application.Bookings.Validators;

public sealed class CreateBookingRequestValidator : AbstractValidator<CreateBookingRequest>
{
    public CreateBookingRequestValidator(BookingOptions options)
    {
        RuleFor(request => request.BranchId).NotEmpty();
        RuleFor(request => request.CourtId).NotEmpty();
        RuleFor(request => request.CustomerName).NotEmpty().MaximumLength(150);
        RuleFor(request => request.PhoneNumber)
            .Matches("^0[0-9]{9,10}$")
            .WithMessage("Phone number must be a valid Vietnam phone number.");
        RuleFor(request => request.BookingDate)
            .Must(date => date >= DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("Booking date must be today or a future date.");
        RuleFor(request => request) 
            .Must(request => TimeOnly.Parse(request.StartTime) < TimeOnly.Parse(request.EndTime))
            .WithMessage("End time must be greater than start time.");
        RuleFor(request => request)
            .Must(request => TimeOnly.Parse(request.EndTime).ToTimeSpan() - TimeOnly.Parse(request.StartTime).ToTimeSpan()
                >= TimeSpan.FromMinutes(options.MinimumDurationMinutes))
            .WithMessage($"Booking duration must be at least {options.MinimumDurationMinutes} minutes.");
        RuleFor(request => request.BookingType)
            .Must(value => BookingTypes.All.Any(allowed => string.Equals(allowed, value, StringComparison.OrdinalIgnoreCase)))
            .WithMessage("Booking type must be Fixed or Flexible.");
        RuleFor(request => request.PaymentMethod)
            .Must(value => PaymentMethods.All.Contains(value))
            .WithMessage("Payment method must be Cash, Momo, or BankTransfer.");
        RuleFor(request => request.Note).MaximumLength(1000);
    }
}