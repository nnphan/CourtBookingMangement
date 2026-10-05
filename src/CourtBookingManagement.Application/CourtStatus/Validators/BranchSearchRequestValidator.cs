using CourtBookingManagement.Application.CourtStatus.DTOs;
using FluentValidation;

namespace CourtBookingManagement.Application.CourtStatus.Validators;

public sealed class BranchSearchRequestValidator : AbstractValidator<BranchSearchRequest>
{
    public BranchSearchRequestValidator()
    {
        RuleFor(request => request.Keyword).MaximumLength(150);
        RuleFor(request => request.City).MaximumLength(100);
        RuleFor(request => request.District).MaximumLength(100);
        RuleFor(request => request.Page).GreaterThan(0);
        RuleFor(request => request.PageSize).InclusiveBetween(1, 100);
        RuleFor(request => request.SortBy)
            .Must(sortBy => string.IsNullOrWhiteSpace(sortBy) ||
                            sortBy.Equals("price_asc", StringComparison.OrdinalIgnoreCase) ||
                            sortBy.Equals("price_desc", StringComparison.OrdinalIgnoreCase))
            .WithMessage("SortBy must be 'price_asc' or 'price_desc'.");
        RuleFor(request => request.MinPrice)
            .GreaterThanOrEqualTo(0)
            .When(request => request.MinPrice.HasValue);
        RuleFor(request => request.MaxPrice)
            .GreaterThanOrEqualTo(0)
            .When(request => request.MaxPrice.HasValue);
        RuleFor(request => request)
            .Must(request => !request.MinPrice.HasValue || !request.MaxPrice.HasValue || request.MinPrice <= request.MaxPrice)
            .WithMessage("MinPrice must be less than or equal to MaxPrice.");
        RuleFor(request => request)
            .Must(request => request.StartTime.HasValue == request.EndTime.HasValue)
            .WithMessage("StartTime and EndTime must be provided together.");
        RuleFor(request => request)
            .Must(request => !request.StartTime.HasValue || request.Date.HasValue)
            .WithMessage("Date is required when filtering by StartTime and EndTime.");
        RuleFor(request => request)
            .Must(request => !request.StartTime.HasValue || request.StartTime.Value < request.EndTime!.Value)
            .WithMessage("StartTime must be earlier than EndTime.");
    }
}