namespace CourtBookingManagement.Application.Bookings.DTOs;

public sealed record BookingPricingRequestDetail(DateOnly BookingDate, TimeOnly StartTime, TimeOnly EndTime);
