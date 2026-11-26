using CourtBookingManagement.Application.Abstractions.Data;
using Dapper;

namespace CourtBookingManagement.Api.Realtime;

public sealed class PlayerMatchConsistencyWorker(
    ISqlConnectionFactory connectionFactory,
    ILogger<PlayerMatchConsistencyWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        do
        {
            try
            {
                using var connection = connectionFactory.CreateConnection();
                await connection.ExecuteAsync(new CommandDefinition(Sql, new { BatchSize = 100 },
                    cancellationToken: stoppingToken));
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Failed to reconcile match rooms.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private const string Sql = """
        WITH candidates AS (
            SELECT pm.id,
                   CASE WHEN pm.booking_id IS NOT NULL AND NOT valid.booking_valid
                        THEN 'CANCELLED' ELSE 'EXPIRED' END AS next_status
            FROM matching.player_matches pm
            LEFT JOIN core.branches br ON br.id = pm.branch_id
            CROSS JOIN LATERAL (
                SELECT EXISTS (
                    SELECT 1
                    FROM booking.bookings bk
                    JOIN customer.customers cu ON cu.id = bk.customer_id
                    JOIN booking.booking_details bd ON bd.booking_id = bk.id
                    JOIN core.courts court ON court.id = bd.court_id
                    WHERE bk.id = pm.booking_id AND bk.branch_id = pm.branch_id
                      AND UPPER(bk.status) = 'CONFIRMED' AND bk.is_active AND bk.deleted_at IS NULL
                      AND cu.user_id = pm.created_by AND cu.is_active AND cu.deleted_at IS NULL
                      AND br.is_active AND br.deleted_at IS NULL
                      AND court.id = pm.court_id AND court.branch_id = pm.branch_id
                      AND court.is_active AND court.deleted_at IS NULL
                      AND bd.booking_date = pm.match_date AND UPPER(bd.status) = 'CONFIRMED'
                      AND bd.start_time <= pm.start_time AND bd.end_time >= pm.end_time
                ) AS booking_valid
            ) valid
            WHERE pm.status IN ('OPEN', 'FULL', 'STARTED')
              AND (
                  (pm.booking_id IS NOT NULL AND NOT valid.booking_valid)
                  OR (pm.status IN ('OPEN', 'FULL') AND
                      ((pm.match_date + pm.start_time)
                        AT TIME ZONE COALESCE(NULLIF(br.time_zone, ''), 'Asia/Ho_Chi_Minh')) <= NOW())
              )
            ORDER BY pm.match_date, pm.start_time, pm.id
            LIMIT @BatchSize
            FOR UPDATE OF pm SKIP LOCKED
        ), closed AS (
            UPDATE matching.player_matches pm
            SET status = candidates.next_status,
                cancelled_at = CASE WHEN candidates.next_status = 'CANCELLED'
                                    THEN NOW() ELSE pm.cancelled_at END,
                updated_at = NOW()
            FROM candidates
            WHERE pm.id = candidates.id
            RETURNING pm.id
        )
        UPDATE matching.match_join_requests request
        SET status = 'EXPIRED', updated_at = NOW()
        FROM closed
        WHERE request.match_id = closed.id AND request.status = 'PENDING';
        """;
}
