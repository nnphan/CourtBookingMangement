using CourtBookingManagement.Application.CourtStatus.DTOs;
using FluentValidation;

namespace CourtBookingManagement.Application.CourtStatus.Validators;

public sealed class CreateBookingRequestValidator : AbstractValidator<CreateBookingRequest>
{
    public CreateBookingRequestValidator()
    {
        RuleFor(x => x.BranchId).NotEmpty(); RuleFor(x => x.CourtId).NotEmpty(); RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.BookingType).NotEmpty().MaximumLength(20);
        RuleFor(x => x).Must(x => x.StartTime < x.EndTime).WithMessage("Start time must be before end time.");
    }
}

public sealed class UpdateBookingRequestValidator : AbstractValidator<UpdateBookingRequest>
{
    public UpdateBookingRequestValidator()
    {
        RuleFor(x => x.CourtId).NotEmpty(); RuleFor(x => x.BookingType).NotEmpty().MaximumLength(20);
        RuleFor(x => x).Must(x => x.StartTime < x.EndTime).WithMessage("Start time must be before end time.");
    }
}

public sealed class CreateCourtBlockRequestValidator : AbstractValidator<CreateCourtBlockRequest>
{
    public CreateCourtBlockRequestValidator()
    {
        RuleFor(x => x.CourtId).NotEmpty(); RuleFor(x => x.Reason).NotEmpty().MaximumLength(255);
        RuleFor(x => x).Must(x => x.StartTime < x.EndTime).WithMessage("Start time must be before end time.");
    }
}

public sealed class CreateEventRequestValidator : AbstractValidator<CreateEventRequest>
{
    public CreateEventRequestValidator()
    {
        RuleFor(x => x.BranchId).NotEmpty(); RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x).Must(x => x.StartTime < x.EndTime).WithMessage("Start time must be before end time.");
    }
}