using CourtBookingManagement.Application.Branches.DTOs.Requests;
using FluentValidation;

namespace CourtBookingManagement.Application.Branches.Validators;

public sealed class CreateBranchRequestValidator : AbstractValidator<CreateBranchRequest>
{
    public CreateBranchRequestValidator()
    {
        RuleFor(request => request.Name).NotEmpty().MaximumLength(150);
        RuleFor(request => request.Address).NotEmpty().MaximumLength(255);
        RuleFor(request => request.City).MaximumLength(100);
        RuleFor(request => request.District).MaximumLength(100);
        RuleFor(request => request.PhoneNumber).MaximumLength(20);
        RuleFor(request => request.TimeZone).NotEmpty().MaximumLength(100);
        RuleFor(request => request.Latitude).InclusiveBetween(-90m, 90m).When(request => request.Latitude.HasValue);
        RuleFor(request => request.Longitude).InclusiveBetween(-180m, 180m).When(request => request.Longitude.HasValue);
        RuleFor(request => request.AmenityIds).NotNull();
        RuleFor(request => request.Images).NotNull();
        RuleFor(request => request.Images).Must(images => images is { Count: <= 20 })
            .WithMessage("A branch can have at most 20 images.");
        RuleFor(request => request.OperatingHours).NotNull();
        RuleForEach(request => request.OperatingHours).ChildRules(hour =>
        {
            hour.RuleFor(item => item.OpenTime).GreaterThanOrEqualTo(TimeSpan.Zero).LessThan(TimeSpan.FromDays(1));
            hour.RuleFor(item => item.CloseTime).GreaterThan(TimeSpan.Zero).LessThan(TimeSpan.FromDays(1));
            hour.RuleFor(item => item.CloseTime).GreaterThan(item => item.OpenTime)
                .WithMessage("Close time must be later than open time.");
        });
    }
}