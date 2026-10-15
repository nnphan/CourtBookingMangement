using System.Data;
using CourtBookingManagement.Application.Abstractions.Data;
using CourtBookingManagement.Application.Bookings.DTOs;
using CourtBookingManagement.Application.Bookings.Interfaces;
using CourtBookingManagement.Application.Common.Enums;
using Dapper;

namespace CourtBookingManagement.Infrastructure.Repositories;

public sealed class BookingRepository(ISqlConnectionFactory connectionFactory) : IBookingRepository
{
    public async Task<Guid?> GetCustomerIdByUserIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<Guid?>(new CommandDefinition("""
            SELECT id
            FROM customer.customers
            WHERE user_id = @UserId
              AND is_active = TRUE
              AND deleted_at IS NULL
            LIMIT 1;
            """, new { UserId = userId }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<MyBookingResponse>> GetMyBookingsAsync(
        Guid customerId,
        CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        var bookings = await connection.QueryAsync<MyBookingResponse>(new CommandDefinition("""
            SELECT
                b.id AS BookingId,
                b.booking_code AS BookingCode,
                b.branch_id AS BranchId,
                br.name AS BranchName,
                bd.court_id AS CourtId,
                COALESCE(NULLIF(c.name, ''), c.court_number::text) AS CourtName,
                bd.booking_date AS BookingDate,
                bd.start_time AS StartTime,
                bd.end_time AS EndTime,
                UPPER(b.booking_type) AS BookingType,
                UPPER(b.status) AS Status,
                UPPER(b.payment_method) AS PaymentMethod,
                b.total_amount AS TotalAmount
            FROM booking.bookings b
            INNER JOIN booking.booking_details bd ON bd.booking_id = b.id
            INNER JOIN core.courts c ON c.id = bd.court_id
            INNER JOIN core.branches br ON br.id = b.branch_id
            WHERE b.customer_id = @CustomerId
              AND b.is_active = TRUE
              AND b.deleted_at IS NULL
              AND UPPER(b.status) IN ('PENDING', 'CONFIRMED')
            ORDER BY bd.booking_date DESC, bd.start_time DESC;
            """, new { CustomerId = customerId }, cancellationToken: cancellationToken));

        return bookings.AsList();
    }

    public async Task<IReadOnlyList<BranchPricingDto>> GetActivePricingsAsync(Guid branchId, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        var pricings = await connection.QueryAsync<BranchPricingDto>(new CommandDefinition("""
            SELECT id AS Id,
                   pricing_type AS PricingType,
                   start_time AS StartTime,
                   end_time AS EndTime,
                   price_per_hour AS PricePerHour
            FROM core.branch_pricings
            WHERE branch_id = @BranchId
              AND is_active = TRUE
            ORDER BY start_time;
            """, new { BranchId = branchId }, cancellationToken: cancellationToken));
        return pricings.AsList();
    }

    public Task<bool> ExistsBranchAsync(Guid branchId, CancellationToken cancellationToken) =>
        ScalarAsync<bool>("SELECT EXISTS (SELECT 1 FROM core.branches WHERE id = @BranchId AND is_active AND deleted_at IS NULL);", new { BranchId = branchId }, cancellationToken);

    public Task<bool> ExistsCourtAsync(Guid courtId, CancellationToken cancellationToken) =>
        ScalarAsync<bool>("SELECT EXISTS (SELECT 1 FROM core.courts WHERE id = @CourtId AND is_active AND deleted_at IS NULL);", new { CourtId = courtId }, cancellationToken);

    public Task<bool> CourtBelongsToBranchAsync(Guid courtId, Guid branchId, CancellationToken cancellationToken) =>
        ScalarAsync<bool>("SELECT EXISTS (SELECT 1 FROM core.courts WHERE id = @CourtId AND branch_id = @BranchId AND is_active AND deleted_at IS NULL);", new { CourtId = courtId, BranchId = branchId }, cancellationToken);

    public Task<bool> HasBookingOverlapAsync(Guid courtId, DateOnly bookingDate, TimeOnly startTime, TimeOnly endTime, CancellationToken cancellationToken) =>
        ScalarAsync<bool>("""
            SELECT EXISTS (
                SELECT 1
                FROM booking.bookings b
                JOIN booking.booking_details bd ON bd.booking_id = b.id
                WHERE bd.court_id = @CourtId
                  AND bd.booking_date = @BookingDate
                  AND b.deleted_at IS NULL
                  AND b.is_active
                  AND LOWER(b.status) <> 'cancelled'
                  AND @StartTime < bd.end_time ::time
                  AND @EndTime > bd.start_time ::time
            );
            """, new { CourtId = courtId, BookingDate = bookingDate, StartTime = startTime, EndTime = endTime }, cancellationToken);

    public async Task<BookingDetailResponse?> CreateBookingAsync(CreateBookingRequest request, decimal calculatedPrice, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);

        await connection.ExecuteAsync(new CommandDefinition(
            "SELECT pg_advisory_xact_lock(hashtextextended(@LockKey, 0));",
            new { LockKey = $"{request.CourtId}:{request.BookingDate:yyyy-MM-dd}" },
            transaction,
            cancellationToken: cancellationToken));

        var branchExists = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS (SELECT 1 FROM core.branches WHERE id = @BranchId AND is_active AND deleted_at IS NULL);",
            new { request.BranchId }, transaction, cancellationToken: cancellationToken));
        var lockedCourtId = await connection.ExecuteScalarAsync<Guid?>(new CommandDefinition(
            "SELECT id FROM core.courts WHERE id = @CourtId AND branch_id = @BranchId AND is_active AND deleted_at IS NULL FOR UPDATE;",
            new { request.CourtId, request.BranchId }, transaction, cancellationToken: cancellationToken));
        if (!branchExists || lockedCourtId is null)
        {
            return null;
        }

        //var overlaps = await HasBookingOverlapAsync(connection, transaction, request, cancellationToken);
        //if (overlaps)
        //{
        //    return null;
        //}

        var customerId = await ResolveCustomerAsync(connection, transaction, request, cancellationToken);
        if (customerId is null)
        {
            return null;
        }

        var bookingId = Guid.NewGuid();
        var bookingType = BookingTypes.All.Single(value => string.Equals(value, request.BookingType, StringComparison.OrdinalIgnoreCase));
        const string insertBookingSql = """
            INSERT INTO booking.bookings
                (id, booking_code, customer_id, branch_id, status, booking_type, payment_method,
                 subtotal_amount, paid_amount, total_amount, notes, is_active, created_at, updated_at)
            VALUES
                (@BookingId,
                 CONCAT('BK', TO_CHAR(clock_timestamp(), 'YYMMDDHH24MISSMS'),
                     UPPER(SUBSTRING(REPLACE(gen_random_uuid()::text, '-', '') FROM 1 FOR 5))),
                 @CustomerId, @BranchId, 'confirmed', @BookingType, @PaymentMethod,
                 @CalculatedPrice, 0, @CalculatedPrice, @Note, TRUE, NOW(), NOW());
            """;
        await connection.ExecuteAsync(new CommandDefinition(insertBookingSql, new
        {
            BookingId = bookingId,
            CustomerId = customerId.Value,
            request.BranchId,
            BookingType = bookingType,
            request.PaymentMethod,
            request.Note,
            CalculatedPrice = calculatedPrice
        }, transaction, cancellationToken: cancellationToken));

        const string insertDetailSql = """
            INSERT INTO booking.booking_details
            (
                id,
                booking_id,
                court_id,
                booking_date,
                start_time,
                end_time,
                slot_range,
                status,
                price_charged
            )
            VALUES
            (
                @DetailId,
                @BookingId,
                @CourtId,
                @BookingDate,
                @StartTime,
                @EndTime,
                NULL,
                'reserved',
                @CalculatedPrice
            );
            """;
        await connection.ExecuteAsync(new CommandDefinition(insertDetailSql, new
        {
            DetailId = Guid.NewGuid(),
            BookingId = bookingId,
            request.CourtId,
            request.BookingDate,
            StartTime = TimeOnly.Parse(request.StartTime),
            EndTime = TimeOnly.Parse(request.EndTime),
            CalculatedPrice = calculatedPrice
        }, transaction, cancellationToken: cancellationToken));

        var response = await GetBookingDetailAsync(connection, transaction, bookingId, cancellationToken);
        if (response is null)
        {
            return null;
        }

        transaction.Commit();
        return response;
    }

    public async Task<BookingDetailResponse?> GetBookingDetailAsync(Guid bookingId, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        return await GetBookingDetailAsync(connection, null, bookingId, cancellationToken);
    }

    private async Task<Guid?> ResolveCustomerAsync(IDbConnection connection, IDbTransaction transaction, CreateBookingRequest request, CancellationToken cancellationToken)
    {
        if (request.CustomerId is Guid customerId)
        {
            return await connection.ExecuteScalarAsync<Guid?>(new CommandDefinition(
                "SELECT id FROM customer.customers WHERE id = @CustomerId AND is_active AND deleted_at IS NULL;",
                new { CustomerId = customerId }, transaction, cancellationToken: cancellationToken));
        }

        await connection.ExecuteAsync(new CommandDefinition(
            "SELECT pg_advisory_xact_lock(hashtextextended(@LockKey, 0));",
            new { LockKey = $"customer-phone:{request.PhoneNumber}" },
            transaction,
            cancellationToken: cancellationToken));

        var existingCustomerId = await connection.ExecuteScalarAsync<Guid?>(new CommandDefinition(
            "SELECT id FROM customer.customers WHERE phone_number = @PhoneNumber AND is_active AND deleted_at IS NULL ORDER BY created_at LIMIT 1 FOR UPDATE;",
            new { request.PhoneNumber }, transaction, cancellationToken: cancellationToken));
        if (existingCustomerId is not null)
        {
            return existingCustomerId;
        }

        var defaultMembershipId = await connection.ExecuteScalarAsync<Guid?>(new CommandDefinition("""
            SELECT id
            FROM customer.membership_levels
            ORDER BY sort_order ASC, min_points ASC, id ASC
            LIMIT 1;
            """, transaction: transaction, cancellationToken: cancellationToken));

        return await connection.ExecuteScalarAsync<Guid>(new CommandDefinition("""
            INSERT INTO customer.customers
                (id, membership_level_id, full_name, phone_number, is_guest,
                 loyalty_points_balance, is_active, created_at, updated_at)
            VALUES
                (@CustomerId, @MembershipLevelId, @CustomerName, @PhoneNumber, TRUE, 0, TRUE, NOW(), NOW())
            RETURNING id;
            """, new
        {
            CustomerId = Guid.NewGuid(),
            MembershipLevelId = defaultMembershipId,
            request.CustomerName,
            request.PhoneNumber
        }, transaction, cancellationToken: cancellationToken));
    }

    private static async Task<bool> HasBookingOverlapAsync(IDbConnection connection, IDbTransaction transaction, CreateBookingRequest request, CancellationToken cancellationToken) =>
        await connection.ExecuteScalarAsync<bool>(new CommandDefinition("""
            SELECT EXISTS (
                SELECT 1
                FROM booking.bookings b
                JOIN booking.booking_details bd ON bd.booking_id = b.id
                WHERE bd.court_id = @CourtId
                  AND bd.booking_date = @BookingDate
                  AND b.deleted_at IS NULL
                  AND b.is_active
                  AND LOWER(b.status) <> 'cancelled'
                  AND @StartTime < bd.end_time ::time
                  AND @EndTime > bd.start_time ::time
            );
            """, new { request.CourtId, request.BookingDate, StartTime = TimeOnly.Parse(request.StartTime), EndTime = TimeOnly.Parse(request.EndTime)  }, transaction, cancellationToken: cancellationToken));

    private static async Task<BookingDetailResponse?> GetBookingDetailAsync(IDbConnection connection, IDbTransaction? transaction, Guid bookingId, CancellationToken cancellationToken) =>
        await connection.QuerySingleOrDefaultAsync<BookingDetailResponse>(new CommandDefinition("""
            SELECT b.id AS BookingId,
                   b.branch_id AS BranchId,
                   bd.court_id AS CourtId,
                   COALESCE(NULLIF(c.name, ''), c.court_number::text) AS CourtName,
                   cu.id AS CustomerId,
                   cu.full_name AS CustomerName,
                   cu.phone_number AS PhoneNumber,
                   bd.booking_date AS BookingDate,
                   bd.start_time AS StartTime,
                   bd.end_time AS EndTime,
                   UPPER(b.booking_type) AS BookingType,
                   UPPER(b.status) AS BookingStatus,
                   b.payment_method AS PaymentMethod
            FROM booking.bookings b
            JOIN booking.booking_details bd ON bd.booking_id = b.id
            JOIN core.courts c ON c.id = bd.court_id
            JOIN customer.customers cu ON cu.id = b.customer_id
            WHERE b.id = @BookingId AND b.is_active AND b.deleted_at IS NULL;
            """, new { BookingId = bookingId }, transaction, cancellationToken: cancellationToken));

    private async Task<T> ScalarAsync<T>(string sql, object parameters, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<T>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }
}