using System.ComponentModel.DataAnnotations;

namespace CourtBookingManagement.Application.Options;

public sealed class BookingOptions
{
    public const string SectionName = "Booking";

    [Range(1, 1440)]
    public int MinimumDurationMinutes { get; set; } = 60;

    [Range(1, 100)]
    public int MaximumDetailsPerBooking { get; set; } = 10;
}