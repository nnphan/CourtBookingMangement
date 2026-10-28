namespace CourtBookingManagement.Application.Bookings.DTOs;

public sealed class CreateStaffBookingRequest : CreateBookingRequest
{
    public Guid? CustomerId { get; init; }
    public string? CustomerName { get; init; }
    public string? PhoneNumber { get; init; }
}
