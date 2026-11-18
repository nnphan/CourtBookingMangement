namespace CourtBookingManagement.Domain.Payments;

public static class PaymentStatuses
{
    public const string Pending = "PENDING";
    public const string Processing = "PROCESSING";
    public const string Paid = "PAID";
    public const string Failed = "FAILED";
    public const string Expired = "EXPIRED";
    public const string PaymentReview = "PAYMENT_REVIEW";
    public const string Underpaid = "UNDERPAID";
    public const string Overpaid = "OVERPAID";
    public const string RefundPending = "REFUND_PENDING";
    public const string Refunded = "REFUNDED";
}
