using System.Data;
using CourtBookingManagement.Application.Abstractions.Data;
using CourtBookingManagement.Application.Sales.Models.Responses;
using CourtBookingManagement.Application.Sales.Repositories;
using Dapper;

namespace CourtBookingManagement.Infrastructure.Repositories;

public sealed class BusinessStatisticsRepository(ISqlConnectionFactory connectionFactory) : IBusinessStatisticsRepository
{
    public async Task<IReadOnlyList<BusinessStatisticsBranch>> GetBranchesAsync(
        Guid userId, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<BusinessStatisticsBranch>(new CommandDefinition("""
            WITH report_roles AS (
                SELECT r.id, r.code
                FROM auth.roles r
                JOIN auth.role_permissions rp ON rp.role_id = r.id
                JOIN auth.permissions p ON p.id = rp.permission_id
                WHERE p.code = 'sales.report.view' AND r.is_active AND r.deleted_at IS NULL
                  AND r.code IN ('BRANCH_OWNER', 'BRANCH_MANAGER')
            ), access AS (
                SELECT b.id, true AS is_owner
                FROM core.branches b
                JOIN core.owners o ON o.id = b.owner_id
                WHERE o.user_id = @UserId AND o.is_active AND o.deleted_at IS NULL
                  AND EXISTS (SELECT 1 FROM report_roles WHERE code = 'BRANCH_OWNER')
                UNION ALL
                SELECT assignment.branch_id, false
                FROM auth.branch_user_roles assignment
                JOIN report_roles r ON r.id = assignment.role_id
                WHERE assignment.user_id = @UserId AND assignment.is_active
                  AND assignment.removed_at IS NULL AND r.code = 'BRANCH_MANAGER'
            )
            SELECT b.id AS Id, b.name AS Name, b.time_zone AS TimeZone, bool_or(a.is_owner) AS IsOwner
            FROM access a
            JOIN core.branches b ON b.id = a.id
            JOIN auth.users u ON u.id = @UserId
            WHERE b.is_active AND b.deleted_at IS NULL AND u.is_active AND u.deleted_at IS NULL
            GROUP BY b.id, b.name, b.time_zone
            ORDER BY b.name, b.id;
            """, new { UserId = userId }, cancellationToken: cancellationToken));
        return rows.AsList();
    }

    private const string BookingSlots = """
        WITH report_slots AS (
            SELECT b.id, b.branch_id, d.court_id, d.booking_date, d.start_time, d.end_time,
                   LOWER(b.status) AS status,
                   LOWER(b.status) IN ('confirmed', 'checked_in', 'completed')
                       AND LOWER(d.status) IN ('confirmed', 'checked_in', 'completed') AS booked,
                   (EXTRACT(EPOCH FROM (d.end_time - d.start_time)) / 3600)::numeric AS hours,
                   CASE WHEN LOWER(b.status) IN ('confirmed', 'checked_in', 'completed')
                         AND LOWER(d.status) IN ('confirmed', 'checked_in', 'completed')
                        THEN COALESCE(b.paid_amount * d.price_charged / NULLIF(b.total_amount, 0), 0)
                        ELSE 0 END AS revenue,
                   ROW_NUMBER() OVER (PARTITION BY b.id ORDER BY d.booking_date, d.start_time, d.id) AS slot_number
            FROM booking.booking_details d
            JOIN booking.bookings b ON b.id = d.booking_id
            WHERE b.branch_id = ANY(@BranchIds) AND d.booking_date BETWEEN @From AND @To
              AND (LOWER(b.status) = 'cancelled' OR (b.is_active AND b.deleted_at IS NULL))
        )

        """;

    private const string SalesOrders = """
        WITH report_orders AS (
            SELECT so.id, so.branch_id, so.completed_at,
                   (so.completed_at AT TIME ZONE b.time_zone)::date AS sale_date,
                   GREATEST(so.paid_amount - COALESCE(refunds.amount, 0), 0) AS revenue,
                   so.subtotal_amount,
                   so.status
            FROM sales.sales_orders so
            JOIN core.branches b ON b.id = so.branch_id
            LEFT JOIN LATERAL (
                SELECT SUM(r.amount) AS amount
                FROM sales.payments p
                JOIN sales.refunds r ON r.payment_id = p.id AND r.status = 'PROCESSED'
                WHERE p.sales_order_id = so.id
            ) refunds ON true
            WHERE so.branch_id = ANY(@BranchIds)
              AND so.status IN ('COMPLETED', 'PARTIALLY_REFUNDED')
              AND so.completed_at >= (@From::date::timestamp AT TIME ZONE b.time_zone)
              AND so.completed_at < ((@To::date + 1)::timestamp AT TIME ZONE b.time_zone)
        )

        """;

    public async Task<BusinessStatisticsResponse> GetStatisticsAsync(
        Guid[] branchIds, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction(IsolationLevel.RepeatableRead);
        using var results = await connection.QueryMultipleAsync(new CommandDefinition(
            BookingSlots + """
            SELECT branch_id AS BranchId, booking_date AS Date,
                   COUNT(*) FILTER (WHERE slot_number = 1 AND status NOT IN ('expired', 'refunded')) AS BookingCount,
                   COUNT(*) FILTER (WHERE slot_number = 1 AND status = 'completed') AS CompletedCount,
                   COUNT(*) FILTER (WHERE slot_number = 1 AND status = 'cancelled') AS CancelledCount,
                   COALESCE(SUM(hours) FILTER (WHERE booked), 0) AS BookedHours,
                   COALESCE(SUM(hours) FILTER (WHERE booked AND status = 'completed'), 0) AS UsedHours,
                   ROUND(SUM(revenue), 2) AS Revenue
            FROM report_slots
            GROUP BY branch_id, booking_date
            ORDER BY booking_date;

            """ + SalesOrders + """
            SELECT branch_id AS BranchId, sale_date AS Date,
                   COUNT(*) AS OrderCount, SUM(revenue) AS Revenue
            FROM report_orders
            GROUP BY branch_id, sale_date
            ORDER BY sale_date;

            """ + BookingSlots + """
            , used AS (
                SELECT court_id, SUM(hours) AS hours
                FROM report_slots WHERE booked AND status = 'completed' GROUP BY court_id
            )
            SELECT c.branch_id AS BranchId,
                   COALESCE(NULLIF(c.name, ''), c.court_number::text) AS CourtName,
                   COALESCE(used.hours, 0) AS UsedHours,
                   COALESCE(schedule.hours, 0) AS AvailableHours
            FROM core.courts c
            JOIN core.branches b ON b.id = c.branch_id
            LEFT JOIN used ON used.court_id = c.id
            LEFT JOIN core.operating_hours oh ON oh.branch_id = c.branch_id AND NOT oh.is_closed
            LEFT JOIN LATERAL (
                SELECT SUM(GREATEST(EXTRACT(EPOCH FROM (
                    LEAST(operating_day.value::date + oh.close_time, (NOW() AT TIME ZONE b.time_zone))
                    - (operating_day.value::date + oh.open_time)
                )) / 3600, 0)) AS hours
                FROM generate_series(
                    GREATEST(@From::date, (c.created_at AT TIME ZONE b.time_zone)::date)::timestamp,
                    LEAST(@To::date, (NOW() AT TIME ZONE b.time_zone)::date,
                          COALESCE((c.deleted_at AT TIME ZONE b.time_zone)::date, @To::date))::timestamp,
                    interval '1 day'
                ) AS operating_day(value)
                WHERE oh.id IS NOT NULL
            ) schedule ON true
            WHERE c.branch_id = ANY(@BranchIds)
              AND ((c.is_active AND c.deleted_at IS NULL) OR used.court_id IS NOT NULL)
              AND (COALESCE(schedule.hours, 0) > 0 OR used.court_id IS NOT NULL)
            ORDER BY b.name, c.court_number;

            """ + BookingSlots + """
            SELECT LEFT(start_time::text, 5) || '–' || LEFT(end_time::text, 5) AS Slot,
                   COUNT(DISTINCT id) AS BookingCount, ROUND(SUM(revenue), 2) AS Revenue
            FROM report_slots WHERE booked
            GROUP BY start_time, end_time
            ORDER BY COUNT(DISTINCT id) DESC, start_time, end_time
            LIMIT 10;

            """ + SalesOrders + """
            SELECT i.product_name_snapshot AS ProductName, COALESCE(category.name, '—') AS Category,
                   SUM(i.quantity) AS Quantity,
                   ROUND(SUM(COALESCE(i.line_total * o.revenue / NULLIF(o.subtotal_amount, 0), 0)), 2) AS Revenue,
                   CASE WHEN BOOL_AND(cost.unit_cost IS NOT NULL AND o.status = 'COMPLETED')
                        THEN SUM(cost.unit_cost * i.quantity) END AS Cost
            FROM report_orders o
            JOIN sales.sales_order_items i ON i.sales_order_id = o.id
            LEFT JOIN catalog.products p ON p.id = i.product_id
            LEFT JOIN catalog.product_categories category ON category.id = p.category_id
            LEFT JOIN LATERAL (
                SELECT ri.unit_cost
                FROM inventory.goods_receipt_items ri
                JOIN inventory.goods_receipts r ON r.id = ri.goods_receipt_id
                WHERE ri.product_id = i.product_id AND r.branch_id = o.branch_id
                  AND r.status = 'POSTED' AND r.posted_at <= o.completed_at
                ORDER BY r.posted_at DESC, r.id DESC
                LIMIT 1
            ) cost ON true
            GROUP BY i.product_id, i.product_name_snapshot, category.name
            ORDER BY SUM(i.quantity) DESC, i.product_name_snapshot;
            """, new { BranchIds = branchIds, From = from, To = to }, transaction,
            cancellationToken: cancellationToken));
        var response = new BusinessStatisticsResponse(
            (await results.ReadAsync<BookingStatisticsDay>()).AsList(),
            (await results.ReadAsync<SalesStatisticsDay>()).AsList(),
            (await results.ReadAsync<CourtUsageStatistics>()).AsList(),
            (await results.ReadAsync<PeakSlotStatistics>()).AsList(),
            (await results.ReadAsync<ProductStatistics>()).AsList());
        transaction.Commit();
        return response;
    }
}
