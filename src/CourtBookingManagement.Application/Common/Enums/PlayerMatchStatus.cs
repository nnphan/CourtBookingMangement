namespace CourtBookingManagement.Application.Common.Enums;

public static class PlayerMatchStatus
{
    public const string Draft = "DRAFT";
    public const string Open = "OPEN";
    public const string Full = "FULL";
    public const string Started = "STARTED";
    public const string Completed = "COMPLETED";
    public const string Cancelled = "CANCELLED";
    public const string Expired = "EXPIRED";

    public static readonly string[] All =
    [
        Draft,
        Open,
        Full,
        Started,
        Completed,
        Cancelled,
        Expired
    ];
}
