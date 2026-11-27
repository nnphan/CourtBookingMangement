using System.Data;
using CourtBookingManagement.Application.Abstractions.Data;
using CourtBookingManagement.Application.Sales.Models.Requests;
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
                   so.status, so.order_code
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
                   ROUND(SUM(revenue), 2) AS Revenue,
                   COUNT(DISTINCT id) FILTER (WHERE status NOT IN ('expired', 'refunded')) AS DailyBookingCount
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
            LEFT JOIN LATERAL (
                SELECT SUM(GREATEST(EXTRACT(EPOCH FROM (
                    LEAST(operating_day.value::date + oh.close_time, (NOW() AT TIME ZONE b.time_zone))
                    - (operating_day.value::date + oh.open_time)
                )) / 3600, 0)) AS hours
                FROM core.operating_hours oh
                CROSS JOIN generate_series(
                    GREATEST(@From::date, (c.created_at AT TIME ZONE b.time_zone)::date)::timestamp,
                    LEAST(@To::date, (NOW() AT TIME ZONE b.time_zone)::date,
                          COALESCE((c.deleted_at AT TIME ZONE b.time_zone)::date, @To::date))::timestamp,
                    interval '1 day'
                ) AS operating_day(value)
                WHERE oh.branch_id = c.branch_id AND NOT oh.is_closed
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
            SELECT o.branch_id AS BranchId, o.sale_date AS Date, i.product_id AS ProductId,
                   MAX(COALESCE(p.name, i.product_name_snapshot)) AS ProductName,
                   COALESCE(category.name, '—') AS Category, MAX(i.unit_snapshot) AS Unit,
                   SUM(i.quantity) AS Quantity,
                   ROUND(SUM(i.quantity * i.unit_price), 2) AS Revenue,
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
            GROUP BY o.branch_id, o.sale_date, i.product_id, category.name
            ORDER BY o.sale_date, ProductName, i.product_id;

            SELECT id AS Id, branch_id AS BranchId, expense_date AS Date, category AS Category,
                   title AS Title, amount AS Amount, notes AS Notes
            FROM sales.operating_expenses
            WHERE branch_id = ANY(@BranchIds) AND expense_date BETWEEN @From AND @To
            ORDER BY expense_date DESC, created_at DESC, id;
            """, new { BranchIds = branchIds, From = from, To = to }, transaction,
            cancellationToken: cancellationToken));
        var response = new BusinessStatisticsResponse(
            (await results.ReadAsync<BookingStatisticsDay>()).AsList(),
            (await results.ReadAsync<SalesStatisticsDay>()).AsList(),
            (await results.ReadAsync<CourtUsageStatistics>()).AsList(),
            (await results.ReadAsync<PeakSlotStatistics>()).AsList(),
            (await results.ReadAsync<ProductStatistics>()).AsList(),
            (await results.ReadAsync<BusinessExpense>()).AsList());
        transaction.Commit();
        return response;
    }

    public async Task<IReadOnlyList<BookingStatisticsDetail>> GetBookingDetailsAsync(
        Guid[] branchIds, DateOnly date, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<BookingStatisticsDetail>(new CommandDefinition("""
            SELECT d.id AS Id, d.booking_date AS Date, b.booking_code AS BookingCode,
                   COALESCE(NULLIF(c.name, ''), c.court_number::text) AS Court,
                   COALESCE(cu.full_name, '') AS Customer, d.start_time AS StartTime, d.end_time AS EndTime,
                   d.price_charged AS Amount, COALESCE(b.payment_method, '') AS PaymentMethod,
                   UPPER(CASE WHEN LOWER(d.status) = 'cancelled' THEN d.status ELSE b.status END) AS Status
            FROM booking.booking_details d
            JOIN booking.bookings b ON b.id = d.booking_id
            JOIN core.courts c ON c.id = d.court_id
            LEFT JOIN customer.customers cu ON cu.id = b.customer_id
            WHERE b.branch_id = ANY(@BranchIds) AND d.booking_date = @Date
              AND (LOWER(b.status) = 'cancelled' OR (b.is_active AND b.deleted_at IS NULL))
              AND LOWER(b.status) NOT IN ('expired', 'refunded')
            ORDER BY d.start_time, b.booking_code, d.id;
            """, new { BranchIds = branchIds, Date = date }, cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<IReadOnlyList<SalesStatisticsDetail>> GetSalesDetailsAsync(
        Guid[] branchIds, DateOnly date, Guid productId, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<SalesStatisticsDetail>(new CommandDefinition(SalesOrders + """
            SELECT i.id AS Id, o.sale_date AS Date, o.order_code AS OrderCode, COALESCE(category.name, '—') AS Category,
                   i.product_name_snapshot AS ProductName, i.unit_snapshot AS Unit, i.quantity AS Quantity,
                   i.unit_price AS UnitPrice, i.quantity * i.unit_price AS Amount
            FROM report_orders o
            JOIN sales.sales_order_items i ON i.sales_order_id = o.id
            JOIN catalog.products p ON p.id = i.product_id
            LEFT JOIN catalog.product_categories category ON category.id = p.category_id
            WHERE i.product_id = @ProductId
            ORDER BY o.completed_at, o.order_code, i.id;
            """, new { BranchIds = branchIds, From = date, To = date, ProductId = productId },
            cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<Guid?> SaveExpenseAsync(
        Guid? id, Guid[] branchIds, Guid userId, BusinessExpenseRequest request, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        var sql = id.HasValue ? """
            UPDATE sales.operating_expenses
            SET branch_id = @BranchId, expense_date = @Date, category = @Category, title = @Title,
                amount = @Amount, notes = @Notes, updated_at = now()
            WHERE id = @Id AND branch_id = ANY(@BranchIds) AND @BranchId = ANY(@BranchIds)
            RETURNING id;
            """ : """
            INSERT INTO sales.operating_expenses (id, branch_id, expense_date, category, title, amount, notes, created_by)
            SELECT @Id, @BranchId, @Date, @Category, @Title, @Amount, @Notes, @UserId
            WHERE @BranchId = ANY(@BranchIds)
            RETURNING id;
            """;
        return await connection.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition(sql, new
        {
            Id = id ?? Guid.NewGuid(), BranchIds = branchIds, UserId = userId, request.BranchId, request.Date,
            request.Category, Title = request.Title.Trim(), request.Amount, Notes = request.Notes?.Trim()
        }, cancellationToken: cancellationToken));
    }

    public async Task<bool> DeleteExpenseAsync(Guid id, Guid[] branchIds, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        return await connection.ExecuteAsync(new CommandDefinition("""
            DELETE FROM sales.operating_expenses WHERE id = @Id AND branch_id = ANY(@BranchIds);
            """, new { Id = id, BranchIds = branchIds }, cancellationToken: cancellationToken)) > 0;
    }
}
