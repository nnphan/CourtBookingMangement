namespace CourtBookingManagement.Domain.Bookings;

public static class BookingStatusTransitions
{
    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> AllowedTransitions =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
        {
            [BookingStatuses.Pending] = new HashSet<string>(StringComparer.Ordinal)
            {
                BookingStatuses.Confirmed,
                BookingStatuses.Cancelled
            },
            [BookingStatuses.PendingPayment] = new HashSet<string>(StringComparer.Ordinal)
            {
                BookingStatuses.Expired,
                BookingStatuses.Cancelled,
                BookingStatuses.PaymentReview
            },
            [BookingStatuses.Confirmed] = new HashSet<string>(StringComparer.Ordinal)
            {
                BookingStatuses.CheckedIn,
                BookingStatuses.Completed,
                BookingStatuses.Cancelled
            },
            [BookingStatuses.CheckedIn] = new HashSet<string>(StringComparer.Ordinal)
            {
                BookingStatuses.Completed,
                BookingStatuses.Cancelled
            },
            [BookingStatuses.Completed] = new HashSet<string>(StringComparer.Ordinal),
            [BookingStatuses.Cancelled] = new HashSet<string>(StringComparer.Ordinal),
            [BookingStatuses.Expired] = new HashSet<string>(StringComparer.Ordinal),
            [BookingStatuses.PaymentReview] = new HashSet<string>(StringComparer.Ordinal)
            {
                BookingStatuses.Confirmed,
                BookingStatuses.Expired,
                BookingStatuses.Cancelled,
                BookingStatuses.Refunded
            },
            [BookingStatuses.Refunded] = new HashSet<string>(StringComparer.Ordinal)
        };

    public static bool CanTransition(string currentStatus, string newStatus) =>
        AllowedTransitions.TryGetValue(currentStatus, out var allowed)
        && allowed.Contains(newStatus);
}
