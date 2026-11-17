using CourtBookingManagement.Application.Bookings.Services;
using CourtBookingManagement.Application.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace CourtBookingManagement.Infrastructure.BackgroundServices;

public sealed class BookingHoldExpirationWorker(
    IServiceScopeFactory serviceScopeFactory,
    IOptions<BookingHoldOptions> options,
    ILogger<BookingHoldExpirationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(
            TimeSpan.FromSeconds(options.Value.ExpirationWorkerIntervalSeconds));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = serviceScopeFactory.CreateAsyncScope();
                var expirationService = scope.ServiceProvider
                    .GetRequiredService<BookingExpirationService>();
                await expirationService.ExpireBatchAsync(
                    options.Value.ExpirationBatchSize,
                    stoppingToken);
            }
            catch (NpgsqlException exception)
            {
                logger.LogError(exception, "Database error while expiring booking holds.");
            }
        }
    }
}
