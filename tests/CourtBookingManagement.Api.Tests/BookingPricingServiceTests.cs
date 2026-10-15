using CourtBookingManagement.Application.Bookings.DTOs;
using CourtBookingManagement.Application.Bookings.Interfaces;
using CourtBookingManagement.Application.Bookings.Services;
using Xunit;

namespace CourtBookingManagement.Api.Tests;

public sealed class BookingPricingServiceTests
{
    private static readonly Guid BranchId = Guid.NewGuid();

    [Fact]
    public async Task CalculatePriceAsync_uses_normal_pricing_on_weekdays()
    {
        var service = CreateService(
            Pricing("NORMAL", "05:00", "17:00", 100m),
            Pricing("PEAK", "17:00", "23:30", 150m),
            Pricing("WEEKEND", "05:30", "23:30", 200m));

        var price = await service.CalculatePriceAsync(
            BranchId, new DateOnly(2026, 10, 13), new TimeOnly(13, 0), new TimeOnly(15, 0), CancellationToken.None);

        Assert.Equal(200m, price);
    }

    [Fact]
    public async Task CalculatePriceAsync_sums_normal_and_peak_pricing_on_weekdays()
    {
        var service = CreateService(
            Pricing("NORMAL", "05:00", "17:00", 100m),
            Pricing("PEAK", "17:00", "23:30", 150m));

        var price = await service.CalculatePriceAsync(
            BranchId, new DateOnly(2026, 10, 13), new TimeOnly(16, 0), new TimeOnly(18, 0), CancellationToken.None);

        Assert.Equal(250m, price);
    }

    [Fact]
    public async Task CalculatePriceAsync_uses_weekend_pricing_on_weekends()
    {
        var service = CreateService(
            Pricing("NORMAL", "05:00", "17:00", 100m),
            Pricing("PEAK", "17:00", "23:30", 150m),
            Pricing("WEEKEND", "05:30", "23:30", 200m));

        var price = await service.CalculatePriceAsync(
            BranchId, new DateOnly(2026, 10, 18), new TimeOnly(16, 0), new TimeOnly(18, 0), CancellationToken.None);

        Assert.Equal(400m, price);
    }

    [Fact]
    public async Task CalculatePriceAsync_rejects_time_outside_pricing_windows()
    {
        var service = CreateService(Pricing("NORMAL", "05:00", "17:00", 100m));

        var exception = await Assert.ThrowsAsync<BookingPricingException>(() =>
            service.CalculatePriceAsync(
                BranchId, new DateOnly(2026, 10, 13), new TimeOnly(4, 0), new TimeOnly(5, 0), CancellationToken.None));

        Assert.Equal("Booking time is not fully covered by branch pricing configuration.", exception.Message);
    }

    private static BookingPricingService CreateService(params BranchPricingDto[] pricings) =>
        new(new StubBookingRepository(pricings));

    private static BranchPricingDto Pricing(string type, string start, string end, decimal price) => new()
    {
        Id = Guid.NewGuid(),
        PricingType = type,
        StartTime = TimeOnly.Parse(start),
        EndTime = TimeOnly.Parse(end),
        PricePerHour = price
    };

    private sealed class StubBookingRepository(IReadOnlyList<BranchPricingDto> pricings) : IBookingRepository
    {
        public Task<IReadOnlyList<BranchPricingDto>> GetActivePricingsAsync(Guid branchId, CancellationToken cancellationToken) =>
            Task.FromResult(pricings);

        public Task<bool> ExistsBranchAsync(Guid branchId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> ExistsCourtAsync(Guid courtId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> CourtBelongsToBranchAsync(Guid courtId, Guid branchId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> HasBookingOverlapAsync(Guid courtId, DateOnly bookingDate, TimeOnly startTime, TimeOnly endTime, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<BookingDetailResponse?> CreateBookingAsync(CreateBookingRequest request, decimal calculatedPrice, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<BookingDetailResponse?> GetBookingDetailAsync(Guid bookingId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}