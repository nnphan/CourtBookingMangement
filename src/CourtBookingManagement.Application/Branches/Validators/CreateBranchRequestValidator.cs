using CourtBookingManagement.Application.Branches.DTOs.Requests;
using FluentValidation;

namespace CourtBookingManagement.Application.Branches.Validators;

public sealed class CreateBranchRequestValidator : AbstractValidator<CreateBranchRequest>
{
    public CreateBranchRequestValidator()
    {
        RuleFor(request => request.Name).NotEmpty().MaximumLength(150);
        RuleFor(request => request.Description).MaximumLength(1000);
        RuleFor(request => request.Address).NotEmpty().MaximumLength(255);
        RuleFor(request => request.City).NotEmpty().MaximumLength(100);
        RuleFor(request => request.District).NotEmpty().MaximumLength(100);
        RuleFor(request => request.PhoneNumber).NotEmpty().MaximumLength(20);
        RuleFor(request => request.TimeZone).NotEmpty().MaximumLength(100);
        RuleFor(request => request.Latitude).InclusiveBetween(-90m, 90m).When(request => request.Latitude.HasValue);
        RuleFor(request => request.Longitude).InclusiveBetween(-180m, 180m).When(request => request.Longitude.HasValue);
        RuleFor(request => request.AmenityIds).NotNull();
        RuleFor(request => request.Images).NotNull();
        RuleFor(request => request.Images).Must(images => images is { Count: <= 20 })
            .WithMessage("A branch can have at most 20 images.");
        RuleFor(request => request.OperatingHours).NotNull();
        RuleFor(request => request.Courts).NotNull();
        RuleFor(request => request.BranchPricings).NotNull();
        RuleForEach(request => request.OperatingHours).ChildRules(hour =>
        {
            hour.RuleFor(item => item.OpenTime).GreaterThanOrEqualTo(TimeSpan.Zero).LessThan(TimeSpan.FromDays(1));
            hour.RuleFor(item => item.CloseTime).GreaterThan(TimeSpan.Zero).LessThan(TimeSpan.FromDays(1));
            hour.RuleFor(item => item.CloseTime).GreaterThan(item => item.OpenTime)
                .WithMessage("Close time must be later than open time.");
        });
        RuleForEach(request => request.Images).ChildRules(image =>
        {
            image.RuleFor(item => item.ImageUrl).NotEmpty().MaximumLength(2048);
            image.RuleFor(item => item.SortOrder).GreaterThanOrEqualTo((short)0);
        });
        RuleFor(request => request.Courts)
            .Must(courts => courts is null || courts.Select(court => court.CourtNumber).Distinct().Count() == courts.Count)
            .WithMessage("Court numbers must be unique within the request.");
        RuleForEach(request => request.Courts).ChildRules(court =>
        {
            court.RuleFor(item => item.CourtNumber).GreaterThan(0);
            court.RuleFor(item => item.Name).NotEmpty().MaximumLength(100);
        });
        RuleForEach(request => request.BranchPricings).ChildRules(pricing =>
        {
            pricing.RuleFor(item => item.PricingType)
                .Must(type => type is "NORMAL" or "PEAK" or "WEEKEND")
                .WithMessage("Pricing type must be NORMAL, PEAK, or WEEKEND.");
            pricing.RuleFor(item => item.PricePerHour).GreaterThan(0);
            pricing.RuleFor(item => item.StartTime).GreaterThanOrEqualTo(TimeSpan.Zero).LessThan(TimeSpan.FromDays(1));
            pricing.RuleFor(item => item.EndTime).GreaterThan(item => item.StartTime)
                .LessThan(TimeSpan.FromDays(1))
                .WithMessage("Pricing end time must be later than start time and before midnight.");
        });
    }
}