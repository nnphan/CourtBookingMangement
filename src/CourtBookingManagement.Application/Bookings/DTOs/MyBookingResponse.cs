namespace CourtBookingManagement.Application.Bookings.DTOs;

public sealed class MyBookingResponse
{
    public Guid BookingId { get; set; }

    public string BookingCode { get; set; } = string.Empty;

    public Guid BranchId { get; set; }

    public string BranchName { get; set; } = string.Empty;

    public Guid CourtId { get; set; }

    public string CourtName { get; set; } = string.Empty;

    public DateOnly BookingDate { get; set; }

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }

    public string BookingType { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string PaymentMethod { get; set; } = string.Empty;

    public decimal TotalAmount { get; set; }
}