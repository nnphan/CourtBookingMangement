namespace CourtBookingManagement.Application.Bookings.DTOs;

public sealed class CalculateBookingPriceRequest
{
    public Guid BranchId { get; set; }
    public DateOnly BookingDate { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
}