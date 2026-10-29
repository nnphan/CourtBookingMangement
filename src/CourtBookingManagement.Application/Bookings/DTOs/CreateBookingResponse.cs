namespace CourtBookingManagement.Application.Bookings.DTOs;

public sealed class CreateBookingResponse
{
    public Guid BookingId { get; set; }
    public string BookingCode { get; set; } = null!;
    public Guid BranchId { get; set; }
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = null!;
    public string PhoneNumber { get; set; } = null!;
    public string BookingType { get; set; } = null!;
    public string BookingStatus { get; set; } = null!;
    public string PaymentMethod { get; set; } = null!;
    public decimal TotalAmount { get; set; }
    public string? Notes { get; set; }
    public List<CreateBookingDetailResponse> Details { get; set; } = [];
}

public sealed class CreateBookingDetailResponse
{
    public Guid CourtId { get; set; }
    public string CourtName { get; set; } = null!;
    public DateOnly BookingDate { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public decimal Price { get; set; }
}
