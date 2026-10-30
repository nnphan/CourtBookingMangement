namespace CourtBookingManagement.Application.Bookings.DTOs;

public sealed class BookingDetailResponse
{
    public Guid BookingId { get; set; }
    public string BookingCode { get; set; } = null!;
    public Guid BranchId { get; set; }
    public string BranchName { get; set; }
    public Guid? CustomerId { get; set; }
    public string CustomerName { get; set; } = null!;
    public string PhoneNumber { get; set; } = null!;
    public string BookingType { get; set; } = null!;
    public string BookingStatus { get; set; } = null!;
    public string PaymentMethod { get; set; } = null!;
    public string PaymentStatus { get; set; } = null!;
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public string? Notes { get; set; }
    public IReadOnlyList<BookingCourtItemResponse> Details { get; set; } = Array.Empty<BookingCourtItemResponse>();
}

public sealed class BookingCourtItemResponse
{
    public Guid BookingDetailId { get; set; }
    public Guid CourtId { get; set; }
    public string CourtName { get; set; } = null!;
    public DateOnly BookingDate { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public decimal Price { get; set; }
}