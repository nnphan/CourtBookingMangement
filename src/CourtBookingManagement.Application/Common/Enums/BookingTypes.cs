namespace CourtBookingManagement.Application.Common.Enums;

public static class BookingTypes
{
    public const string Fixed = "Fixed";
    public const string Flexible = "Flexible";

    public static readonly string[] All =
    [
        Fixed,
        Flexible
    ];
}