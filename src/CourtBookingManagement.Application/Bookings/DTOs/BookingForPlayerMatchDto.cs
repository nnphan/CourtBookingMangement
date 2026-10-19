namespace CourtBookingManagement.Application.Bookings.DTOs;

public sealed class BookingForPlayerMatchDto
{
    public Guid BranchId { get; set; }

    public string Status { get; set; } = string.Empty;

    public Guid CourtId { get; set; }

    public DateOnly BookingDate { get; set; }

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }
}