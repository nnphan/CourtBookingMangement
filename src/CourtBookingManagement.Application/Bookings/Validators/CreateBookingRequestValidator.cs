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
        When(request => !string.IsNullOrWhiteSpace(request.CustomerName) || !string.IsNullOrWhiteSpace(request.PhoneNumber), () =>
        {
            RuleFor(request => request.CustomerName).NotEmpty().MaximumLength(150);
            RuleFor(request => request.PhoneNumber)
                .Must(value => value is not null && System.Text.RegularExpressions.Regex.IsMatch(value, "^0[0-9]{9,10}$"))
                .WithMessage("Phone number must be a valid Vietnam phone number.");
        });
        RuleFor(request => request.BookingType)
            .Must(value => BookingTypes.AllowedForBookings.Any(allowed => string.Equals(allowed, value, StringComparison.OrdinalIgnoreCase)))
            .WithMessage("Booking type must be Fixed, Flexible, or multi_court.");
        RuleFor(request => request.PaymentMethod)
            .Must(value => value is not null && PaymentMethods.All.Contains(value))
            .WithMessage("Payment method must be Cash, Momo, or BankTransfer.");
        RuleFor(request => request.Notes).MaximumLength(1000);
        RuleFor(request => request.Details)
            .NotEmpty()
            .WithMessage("At least one booking detail is required.");
        RuleForEach(request => request.Details)
            .SetValidator(new CreateBookingDetailRequestValidator(options));
    }
}

public sealed class CreateBookingDetailRequestValidator : AbstractValidator<CreateBookingDetailRequest>
{
    public CreateBookingDetailRequestValidator(BookingOptions options)
    {
        RuleFor(detail => detail.CourtId).NotEmpty();
        RuleFor(detail => detail.BookingDate)
            .Must(date => date >= DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("Booking date must be today or a future date.");
        RuleFor(detail => detail.StartTime)
            .Must(value => TimeOnly.TryParse(value, out _))
            .WithMessage("Start time must be a valid time (HH:mm).");
        RuleFor(detail => detail.EndTime)
            .Must(value => TimeOnly.TryParse(value, out _))
            .WithMessage("End time must be a valid time (HH:mm).");

        When(detail => TimeOnly.TryParse(detail.StartTime, out _) && TimeOnly.TryParse(detail.EndTime, out _), () =>
        {
            RuleFor(detail => detail)
                .Must(detail => TimeOnly.Parse(detail.StartTime) < TimeOnly.Parse(detail.EndTime))
                .WithMessage("End time must be greater than start time.");
            RuleFor(detail => detail)
                .Must(detail => TimeOnly.Parse(detail.EndTime).ToTimeSpan() - TimeOnly.Parse(detail.StartTime).ToTimeSpan()
                    >= TimeSpan.FromMinutes(options.MinimumDurationMinutes))
                .WithMessage($"Booking duration must be at least {options.MinimumDurationMinutes} minutes.");
        });
    }
}