namespace CourtBookingManagement.Application.Bookings.Services;

public sealed class BookingPricingException(string message) : Exception(message)
{
}