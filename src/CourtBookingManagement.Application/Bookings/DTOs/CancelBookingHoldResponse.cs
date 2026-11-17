namespace CourtBookingManagement.Application.Bookings.DTOs;

public sealed record CancelBookingHoldResponse(Guid BookingId, string Status);
