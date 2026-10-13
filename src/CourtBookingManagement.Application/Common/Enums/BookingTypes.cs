namespace CourtBookingManagement.Application.Common.Enums;

public static class BookingTypes
{
    public const string Fixed = "FIXED";
    public const string Flexible = "FLEXIBLE";

    public static readonly string[] All =
    [
        Fixed,
        Flexible
    ];
}