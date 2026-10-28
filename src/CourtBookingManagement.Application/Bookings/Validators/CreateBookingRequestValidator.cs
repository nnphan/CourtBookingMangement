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
        RuleFor(request => request.BookingType)
            .Must(value => BookingTypes.All.Contains(value, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"Booking type must be {string.Join(" or ", BookingTypes.All)}.");
        RuleFor(request => request.PaymentMethod)
            .Must(value => PaymentMethods.All.Contains(value, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"Payment method must be {string.Join(", ", PaymentMethods.All)}.");
        RuleFor(request => request.Notes).MaximumLength(1000);

        RuleFor(request => request.Details)
            .NotNull()
            .WithMessage("At least one booking detail is required.")
            .Must(details => details is { Count: > 0 })
            .WithMessage("At least one booking detail is required.")
            .Must(details => details is null || details.Count <= options.MaximumDetailsPerBooking)
            .WithErrorCode("BOOKING_DETAILS_LIMIT_EXCEEDED")
            .WithMessage($"A booking can contain at most {options.MaximumDetailsPerBooking} details.");

        RuleForEach(request => request.Details).ChildRules(detail =>
        {
            detail.RuleFor(item => item.CourtId).NotEmpty();
            detail.RuleFor(item => item.BookingDate).Must(date => date != DateOnly.MinValue);
            detail.RuleFor(item => item)
                .Must(item => item.EndTime > item.StartTime)
                .WithErrorCode("BOOKING_INVALID_TIME_RANGE")
                .WithMessage("End time must be later than start time.");
            detail.RuleFor(item => item)
                .Must(item => item.EndTime > item.StartTime
                    && item.EndTime - item.StartTime >= TimeSpan.FromMinutes(options.MinimumDurationMinutes))
                .WithMessage($"Booking duration must be at least {options.MinimumDurationMinutes} minutes.");
        });

        RuleFor(request => request.Details).Custom((details, context) =>
        {
            if (details is null)
            {
                return;
            }

            var uniqueDetails = new HashSet<(Guid CourtId, DateOnly BookingDate, TimeOnly StartTime, TimeOnly EndTime)>();
            foreach (var detail in details)
            {
                if (!uniqueDetails.Add((detail.CourtId, detail.BookingDate, detail.StartTime, detail.EndTime)))
                {
                    context.AddFailure(new FluentValidation.Results.ValidationFailure(
                        nameof(CreateBookingRequest.Details),
                        "The request contains a duplicate booking detail.")
                    {
                        ErrorCode = "BOOKING_DUPLICATE_DETAIL"
                    });
                    break;
                }
            }

            foreach (var courtDay in details.GroupBy(detail => (detail.CourtId, detail.BookingDate)))
            {
                var previousEnd = TimeOnly.MinValue;
                foreach (var detail in courtDay.OrderBy(item => item.StartTime).ThenBy(item => item.EndTime))
                {
                    if (detail.StartTime < previousEnd)
                    {
                        context.AddFailure(new FluentValidation.Results.ValidationFailure(
                            nameof(CreateBookingRequest.Details),
                            "Booking details for the same court and date cannot overlap.")
                        {
                            ErrorCode = "BOOKING_REQUEST_DETAILS_OVERLAP"
                        });
                        return;
                    }

                    if (detail.EndTime > previousEnd)
                    {
                        previousEnd = detail.EndTime;
                    }
                }
            }
        });
    }
}
