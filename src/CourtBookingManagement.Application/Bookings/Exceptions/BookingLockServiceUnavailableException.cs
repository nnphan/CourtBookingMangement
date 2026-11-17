namespace CourtBookingManagement.Application.Bookings.Exceptions;

public sealed class BookingLockServiceUnavailableException(Exception innerException)
    : Exception("The booking lock service is unavailable.", innerException);
