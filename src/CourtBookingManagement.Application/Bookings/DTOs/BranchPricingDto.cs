namespace CourtBookingManagement.Application.Bookings.DTOs;

public sealed class BranchPricingDto
{
    public Guid Id { get; set; }

    public string PricingType { get; set; } = null!;

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }

    public decimal PricePerHour { get; set; }
}