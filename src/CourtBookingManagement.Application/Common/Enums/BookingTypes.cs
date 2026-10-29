namespace CourtBookingManagement.Application.Common.Enums;

public static class BookingTypes
{
    public const string Fixed = "FIXED";
    public const string Flexible = "FLEXIBLE";
    public const string MultiCourt = "MULTI_COURT";

    public static readonly string[] All =
    [
        Fixed,
        Flexible
    ];

    public static readonly string[] AllowedForBookings =
    [
        Fixed,
        Flexible,
        MultiCourt
    ];
}