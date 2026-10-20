namespace CourtBookingManagement.Application.Common.Enums;

public static class JoinRequestStatus
{
    public const string Pending = "PENDING";
    public const string Approved = "APPROVED";
    public const string Rejected = "REJECTED";
    public const string Cancelled = "CANCELLED";

    public static readonly string[] All =
    [
        Pending,
        Approved,
        Rejected,
        Cancelled
    ];
}
