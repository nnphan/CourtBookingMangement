namespace CourtBookingManagement.Application.Bookings.DTOs;

public sealed class UpdateBookingStatusRequest
{
    public string Status { get; init; } = string.Empty;
}

public sealed class UpdateBookingStatusResponse
{
    public Guid BookingId { get; init; }
    public string BookingCode { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; init; }
}
