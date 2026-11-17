using System.Text.Json;
using CourtBookingManagement.Application.Bookings.Exceptions;
using CourtBookingManagement.Application.Bookings.Interfaces;
using CourtBookingManagement.Application.Bookings.Models;
using CourtBookingManagement.Domain.Bookings;
using StackExchange.Redis;

namespace CourtBookingManagement.Infrastructure.Redis;

public sealed class RedisBookingHoldCache(IConnectionMultiplexer connectionMultiplexer)
    : IBookingHoldCache
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IDatabase _database = connectionMultiplexer.GetDatabase();

    public async Task SetAsync(
        BookingHoldCacheEntry entry,
        IEnumerable<BookingSlotResource> resources,
        TimeSpan timeToLive,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (timeToLive <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeToLive), "Hold cache lifetime must be positive.");
        }

        var payload = JsonSerializer.Serialize(entry, JsonOptions);
        var keys = resources
            .Select(resource => (RedisKey)BookingHoldRedisKeys.Slot(resource))
            .Append(BookingHoldRedisKeys.Booking(entry.BookingId))
            .Distinct()
            .ToArray();
        var writes = keys.Select(key => _database.StringSetAsync(key, payload, timeToLive));
        try
        {
            await Task.WhenAll(writes);
        }
        catch (Exception exception) when (exception is RedisException or TimeoutException)
        {
            throw new BookingHoldCacheUnavailableException(exception);
        }
    }

    public async Task<BookingHoldCacheEntry?> GetAsync(
        Guid bookingId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var payload = await _database.StringGetAsync(BookingHoldRedisKeys.Booking(bookingId));
        if (payload.IsNull)
        {
            return null;
        }

        return JsonSerializer.Deserialize<BookingHoldCacheEntry>(payload!, JsonOptions)
            ?? throw new InvalidDataException($"Redis returned an invalid booking hold cache entry for {bookingId}.");
    }

    public async Task RemoveAsync(
        Guid bookingId,
        IEnumerable<BookingSlotResource> resources,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var keys = resources
            .Select(resource => (RedisKey)BookingHoldRedisKeys.Slot(resource))
            .Append(BookingHoldRedisKeys.Booking(bookingId))
            .Distinct()
            .ToArray();
        try
        {
            await _database.KeyDeleteAsync(keys);
        }
        catch (Exception exception) when (exception is RedisException or TimeoutException)
        {
            throw new BookingHoldCacheUnavailableException(exception);
        }
    }
}
