namespace CourtBookingManagement.Application.Bookings.DTOs;

public sealed class BookingDetailResponse
{
    public Guid BookingId { get; set; }
    public Guid BranchId { get; set; }
    public Guid CourtId { get; set; }
    public string CourtName { get; set; } = null!;
    public Guid? CustomerId { get; set; }
    public string CustomerName { get; set; } = null!;
    public string PhoneNumber { get; set; } = null!;
    public DateOnly BookingDate { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public string BookingType { get; set; } = null!;
    public string BookingStatus { get; set; } = null!;
    public string PaymentMethod { get; set; } = null!;
}