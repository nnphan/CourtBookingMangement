namespace CourtBookingManagement.Application.Bookings.Exceptions;

public sealed class BookingHoldCacheUnavailableException(Exception innerException)
    : Exception("The booking hold cache is unavailable.", innerException);
