using CourtBookingManagement.Application.Bookings.Interfaces;

namespace CourtBookingManagement.Application.Bookings.Services;

public sealed class BookingPricingService(IBookingRepository repository) : IBookingPricingService
{
    public async Task<decimal> CalculatePriceAsync(
        Guid branchId,
        DateOnly bookingDate,
        TimeOnly startTime,
        TimeOnly endTime,
        CancellationToken cancellationToken)
    {
        if (startTime >= endTime)
        {
            throw new BookingPricingException("Booking end time must be later than start time.");
        }

        var activePricings = await repository.GetActivePricingsAsync(branchId, cancellationToken);
        if (activePricings.Count == 0)
        {
            throw new BookingPricingException("Branch pricing configuration not found.");
        }

        var isWeekend = bookingDate.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
        var eligiblePricings = activePricings
            .Where(pricing => isWeekend
                ? string.Equals(pricing.PricingType, "WEEKEND", StringComparison.OrdinalIgnoreCase)
                : string.Equals(pricing.PricingType, "NORMAL", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(pricing.PricingType, "PEAK", StringComparison.OrdinalIgnoreCase))
            .OrderBy(pricing => pricing.StartTime)
            .ToArray();

        if (eligiblePricings.Length == 0)
        {
            throw new BookingPricingException("Branch pricing configuration not found.");
        }

        var coveredUntil = startTime;
        var price = 0m;

        foreach (var pricing in eligiblePricings)
        {
            var overlapStart = startTime > pricing.StartTime ? startTime : pricing.StartTime;
            var overlapEnd = endTime < pricing.EndTime ? endTime : pricing.EndTime;
            if (overlapStart >= overlapEnd)
            {
                continue;
            }

            if (overlapStart > coveredUntil)
            {
                throw new BookingPricingException("Unable to calculate price for selected duration.");
            }

            var durationHours = (decimal)(overlapEnd - overlapStart).Ticks / TimeSpan.TicksPerHour;
            price += durationHours * pricing.PricePerHour;
            if (overlapEnd > coveredUntil)
            {
                coveredUntil = overlapEnd;
            }
        }

        if (coveredUntil < endTime)
        {
            throw new BookingPricingException("Unable to calculate price for selected duration.");
        }

        price = decimal.Round(price, 2, MidpointRounding.AwayFromZero);
        if (price <= 0)
        {
            throw new BookingPricingException("Calculated booking price must be greater than zero.");
        }

        return price;
    }
}