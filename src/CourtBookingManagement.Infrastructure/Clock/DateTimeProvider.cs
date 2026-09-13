using CourtBookingManagement.Application.Abstractions.Clock;

namespace CourtBookingManagement.Infrastructure.Clock;

internal sealed class DateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}