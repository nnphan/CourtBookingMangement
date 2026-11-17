namespace CourtBookingManagement.Domain.Bookings;

public static class BookingStatuses
{
    public const string Pending = "pending";
    public const string PendingPayment = "pending_payment";
    public const string Confirmed = "confirmed";
    public const string CheckedIn = "checked_in";
    public const string Completed = "completed";
    public const string Cancelled = "cancelled";
    public const string Expired = "expired";
    public const string PaymentReview = "payment_review";
    public const string Refunded = "refunded";

    public static bool IsSupported(string status) =>
        status is Pending or PendingPayment or Confirmed or CheckedIn or Completed or Cancelled or Expired or PaymentReview or Refunded;

    public static string Normalize(string status) => status.Trim().ToLowerInvariant();

    public static string ToApiValue(string status) => status.ToUpperInvariant();
}
