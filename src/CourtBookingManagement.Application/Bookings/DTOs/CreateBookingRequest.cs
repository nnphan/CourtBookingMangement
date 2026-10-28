namespace CourtBookingManagement.Application.Bookings.DTOs;

public class CreateBookingRequest
{
    public Guid BranchId { get; init; }
    public string BookingType { get; init; } = string.Empty;
    public string PaymentMethod { get; init; } = string.Empty;
    public string? Notes { get; init; }
    public IReadOnlyCollection<CreateBookingDetailRequest>? Details { get; init; } = Array.Empty<CreateBookingDetailRequest>();
}

public sealed class CreateBookingDetailRequest
{
    public Guid CourtId { get; init; }
    public DateOnly BookingDate { get; init; }
    public TimeOnly StartTime { get; init; }
    public TimeOnly EndTime { get; init; }
}
