namespace CourtBookingManagement.Application.Bookings.DTOs;

public sealed class CreateBookingRequest
{
    public Guid BranchId { get; set; }
    public Guid CourtId { get; set; }
    public Guid? CustomerId { get; set; }
    public string CustomerName { get; set; } = null!;
    public string PhoneNumber { get; set; } = null!;
    public DateOnly BookingDate { get; set; }
    public string StartTime { get; set; }
    public string EndTime { get; set; }
    public string BookingType { get; set; } = null!;
    public string PaymentMethod { get; set; } = null!;
    public string? Note { get; set; }
}