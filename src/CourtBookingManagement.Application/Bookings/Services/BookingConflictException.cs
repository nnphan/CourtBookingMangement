namespace CourtBookingManagement.Application.Bookings.Services;

public sealed class BookingConflictException : Exception
{
    public BookingConflictException() : base("A booking reservation conflicts with an existing reservation.")
    {
    }
}
