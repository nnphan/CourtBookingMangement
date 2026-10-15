namespace CourtBookingManagement.Application.Bookings.DTOs;

public sealed class CalculateBookingPriceResponse
{
    public Guid BranchId { get; init; }
    public DateOnly BookingDate { get; init; }
    public TimeOnly StartTime { get; init; }
    public TimeOnly EndTime { get; init; }
    public decimal DurationHours { get; init; }
    public decimal SubtotalAmount { get; init; }
    public decimal PaidAmount { get; init; }
    public decimal TotalAmount { get; init; }
}