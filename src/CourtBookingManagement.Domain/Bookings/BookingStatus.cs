namespace CourtBookingManagement.Domain.Bookings;

public enum BookingStatus
{
    Pending,
    Confirmed,
    CheckedIn,
    Completed,
    Cancelled,
    Refunded
}