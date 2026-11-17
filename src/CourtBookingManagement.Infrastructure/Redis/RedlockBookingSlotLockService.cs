using CourtBookingManagement.Application.Bookings.Exceptions;
using CourtBookingManagement.Application.Bookings.Interfaces;
using CourtBookingManagement.Application.Options;
using CourtBookingManagement.Domain.Bookings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedLockNet;
using RedLockNet.SERedis;
using StackExchange.Redis;

namespace CourtBookingManagement.Infrastructure.Redis;

public sealed class RedlockBookingSlotLockService(
    RedLockFactory redLockFactory,
    IOptions<BookingHoldOptions> options,
    ILogger<RedlockBookingSlotLockService> logger) : IBookingSlotLockService
{
    public async Task<IBookingSlotLockHandle?> AcquireAsync(
        IEnumerable<BookingSlotResource> resources,
        CancellationToken cancellationToken)
    {
        var orderedResources = resources
            .Select(resource => resource.ToLockKey())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(resource => resource, StringComparer.Ordinal)
            .ToArray();
        var acquiredLocks = new List<IAsyncDisposable>(orderedResources.Length);
        var handleReturned = false;

        try
        {
            foreach (var resource in orderedResources)
            {
                var lockWaitMilliseconds = Math.Min(
                    options.Value.LockWaitMilliseconds,
                    options.Value.LockRetryCount * options.Value.LockRetryMilliseconds);
                var redLock = await redLockFactory.CreateLockAsync(
                    resource,
                    TimeSpan.FromSeconds(options.Value.LockExpirySeconds),
                    TimeSpan.FromMilliseconds(lockWaitMilliseconds),
                    TimeSpan.FromMilliseconds(options.Value.LockRetryMilliseconds),
                    cancellationToken);

                if (!redLock.IsAcquired)
                {
                    var status = redLock.Status;
                    await redLock.DisposeAsync();
                    if (status == RedLockStatus.Conflicted)
                    {
                        logger.LogInformation("Booking slot lock is already held. Resource: {Resource}", resource);
                        return null;
                    }

                    logger.LogError(
                        "RedLock did not obtain a quorum for a booking slot. Resource: {Resource}, Status: {Status}",
                        resource,
                        status);
                    throw new BookingLockServiceUnavailableException(
                        new InvalidOperationException($"RedLock acquisition ended with status {status}."));
                }

                acquiredLocks.Add(redLock);
            }

            var handle = new BookingSlotLockHandle(acquiredLocks);
            handleReturned = true;
            return handle;
        }
        catch (Exception exception) when (exception is RedisException or TimeoutException)
        {
            logger.LogError(exception, "Redis failed while acquiring booking slot locks.");
            throw new BookingLockServiceUnavailableException(exception);
        }
        finally
        {
            if (!handleReturned)
            {
                for (var index = acquiredLocks.Count - 1; index >= 0; index--)
                {
                    await acquiredLocks[index].DisposeAsync();
                }
            }
        }
    }
}
