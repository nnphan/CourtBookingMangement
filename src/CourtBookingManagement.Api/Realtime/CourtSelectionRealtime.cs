using System.Diagnostics;
using System.Diagnostics.Metrics;
using CourtBookingManagement.Application.Bookings.DTOs;
using CourtBookingManagement.Infrastructure.Repositories;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace CourtBookingManagement.Api.Realtime;

public sealed class CourtStatusHub(CourtSelectionRepository repository) : Hub
{
    public async Task JoinCourtStatus(Guid branchId, DateOnly businessDate)
    {
        if (businessDate == default || !await repository.IsPublicBranchAsync(branchId, Context.ConnectionAborted))
            throw new HubException("BRANCH_ACCESS_DENIED");

        var group = Group(branchId, businessDate);
        if (Context.Items.TryGetValue(nameof(CourtStatusHub), out var current) && current is string previous && previous != group)
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, previous, Context.ConnectionAborted);
        await Groups.AddToGroupAsync(Context.ConnectionId, group, Context.ConnectionAborted);
        Context.Items[nameof(CourtStatusHub)] = group;
    }

    public Task LeaveCourtStatus(Guid branchId, DateOnly businessDate) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, Group(branchId, businessDate), Context.ConnectionAborted);

    public static string Group(Guid branchId, DateOnly businessDate) =>
        FormattableString.Invariant($"court-status:{branchId:N}:{businessDate:yyyy-MM-dd}");
}

public sealed class CourtSelectionPublisher(IHubContext<CourtStatusHub> hub, ILogger<CourtSelectionPublisher> logger)
{
    public const string SlotHeld = "SlotHeld";
    public const string SlotReleased = "SlotReleased";
    public const string SlotsBooked = "SlotsBooked";
    public const string Invalidated = "CourtStatusInvalidated";

    public async Task PublishAsync(CourtSelectionChanged change, string eventName)
    {
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await hub.Clients.Group(CourtStatusHub.Group(change.BranchId, change.BusinessDate))
                .SendAsync(eventName, change, timeout.Token);
        }
        catch (Exception exception)
        {
            CourtSelectionMetrics.PublishFailures.Add(1);
            logger.LogWarning(exception, "Court status publication failed for branch {BranchId} on {BusinessDate}",
                change.BranchId, change.BusinessDate);
        }
    }
}

internal static class CourtSelectionMetrics
{
    private static readonly Meter Meter = new("CourtBookingManagement.CourtSelection");
    internal static readonly Counter<long> Mutations = Meter.CreateCounter<long>("court_selection.mutations");
    internal static readonly Counter<long> Expired = Meter.CreateCounter<long>("court_selection.expired");
    internal static readonly Counter<long> PublishFailures = Meter.CreateCounter<long>("court_selection.publish_failures");
    internal static readonly Histogram<double> CleanupDuration = Meter.CreateHistogram<double>("court_selection.cleanup.duration", "ms");
}

public sealed class CourtSelectionWorker(
    IServiceScopeFactory scopes, CourtSelectionPublisher publisher,
    IOptions<CourtSelectionOptions> options, ILogger<CourtSelectionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.Value.CleanupIntervalSeconds));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                var start = Stopwatch.GetTimestamp();
                using var scope = scopes.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<CourtSelectionRepository>();
                var changes = await repository.ExpireAsync(stoppingToken);
                CourtSelectionMetrics.Expired.Add(changes.Count);
                CourtSelectionMetrics.CleanupDuration.Record(Stopwatch.GetElapsedTime(start).TotalMilliseconds);
                if (changes.Count > 0)
                    logger.LogInformation("Expired {Count} court selection holds", changes.Count);
                await Task.WhenAll(changes.Distinct().Select(change =>
                    publisher.PublishAsync(change, CourtSelectionPublisher.SlotReleased)));
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(exception, "Court selection expiration failed");
            }
        }
    }
}
