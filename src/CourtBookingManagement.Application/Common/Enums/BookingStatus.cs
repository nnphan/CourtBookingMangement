namespace CourtBookingManagement.Application.Common.Enums;

public static class BookingStatus
{
    public const string Pending = "PENDING";
    public const string Confirmed = "CONFIRMED";
    public const string CheckedIn = "CHECKED_IN";
    public const string Completed = "COMPLETED";
    public const string Cancelled = "CANCELLED";
    public const string Refunded = "REFUNDED";

    public static readonly string[] All =
    [
        Pending,
        Confirmed,
        CheckedIn,
        Completed,
        Cancelled,
        Refunded
    ];
}
