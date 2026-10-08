namespace CourtBookingManagement.Application.Branches.DTOs.Requests;

public sealed class CreateBranchPricingRequest
{
    public string PricingType { get; set; } = default!;

    public TimeSpan StartTime { get; set; }

    public TimeSpan EndTime { get; set; }

    public decimal PricePerHour { get; set; }
}