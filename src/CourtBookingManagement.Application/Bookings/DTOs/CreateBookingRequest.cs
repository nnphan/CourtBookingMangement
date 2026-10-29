namespace CourtBookingManagement.Application.Bookings.DTOs;

public sealed class CreateBookingRequest
{
    public Guid BranchId { get; set; }
    public string BookingType { get; set; } = null!;
    public string PaymentMethod { get; set; } = null!;
    public string? Notes { get; set; }
    public List<CreateBookingDetailRequest> Details { get; set; } = [];

    // Guest contact info. Ignored when the caller is a logged-in user with a customer profile.
    public string? CustomerName { get; set; }
    public string? PhoneNumber { get; set; }
}

public sealed class CreateBookingDetailRequest
{
    public Guid CourtId { get; set; }
    public DateOnly BookingDate { get; set; }
    public string StartTime { get; set; } = null!;
    public string EndTime { get; set; } = null!;
}