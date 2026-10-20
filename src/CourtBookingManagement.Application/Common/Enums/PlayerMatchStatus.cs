namespace CourtBookingManagement.Application.Common.Enums;

public static class PlayerMatchStatus
{
    public const string Open = "OPEN";
    public const string Full = "FULL";
    public const string Completed = "COMPLETED";
    public const string Cancelled = "CANCELLED";

    public static readonly string[] All =
    [
        Open,
        Full,
        Completed,
        Cancelled
    ];
}
