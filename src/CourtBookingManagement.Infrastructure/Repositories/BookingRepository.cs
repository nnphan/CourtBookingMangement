using System.Data;
using CourtBookingManagement.Application.Abstractions.Data;
using CourtBookingManagement.Application.Bookings.DTOs;
using CourtBookingManagement.Application.Bookings.Interfaces;
using CourtBookingManagement.Application.Common.Enums;
using CourtBookingManagement.Domain.Bookings;
using Dapper;

namespace CourtBookingManagement.Infrastructure.Repositories;

public sealed class BookingRepository(ISqlConnectionFactory connectionFactory) : IBookingRepository
{
    public async Task<IReadOnlyList<BookingForPlayerMatchDto>> GetPlayerMatchBookingAsync(
        Guid bookingId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        var bookingRows = await connection.QueryAsync<BookingForPlayerMatchDto>(new CommandDefinition("""
            SELECT
                b.branch_id AS BranchId,
                UPPER(b.status) AS Status,
                bd.court_id AS CourtId,
                bd.booking_date AS BookingDate,
                bd.start_time AS StartTime,
                bd.end_time AS EndTime
            FROM booking.bookings b
            INNER JOIN customer.customers c ON c.id = b.customer_id
            INNER JOIN booking.booking_details bd ON bd.booking_id = b.id
            WHERE b.id = @BookingId
              AND c.user_id = @UserId
              AND c.is_active = TRUE
              AND c.deleted_at IS NULL
              AND b.is_active = TRUE
              AND b.deleted_at IS NULL;
            """, new { BookingId = bookingId, UserId = userId }, cancellationToken: cancellationToken));

        return bookingRows.AsList();
    }

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

    public async Task<CreateBookingResponse?> CreateBookingAsync(CreateBookingRequest request, IReadOnlyList<decimal> detailPrices, Guid? knownCustomerId, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);

        // Lock in a stable order so concurrent multi-court requests cannot deadlock.
        var lockKeys = request.Details
            .Select(detail => $"{detail.CourtId}:{detail.BookingDate:yyyy-MM-dd}")
            .Distinct()
            .OrderBy(key => key, StringComparer.Ordinal);
        foreach (var lockKey in lockKeys)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "SELECT pg_advisory_xact_lock(hashtextextended(@LockKey, 0));",
                new { LockKey = lockKey },
                transaction,
                cancellationToken: cancellationToken));
        }

        var branchExists = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS (SELECT 1 FROM core.branches WHERE id = @BranchId AND is_active AND deleted_at IS NULL);",
            new { request.BranchId }, transaction, cancellationToken: cancellationToken));
        if (!branchExists)
        {
            return null;
        }

        foreach (var courtId in request.Details.Select(detail => detail.CourtId).Distinct().OrderBy(id => id))
        {
            var lockedCourtId = await connection.ExecuteScalarAsync<Guid?>(new CommandDefinition(
                "SELECT id FROM core.courts WHERE id = @CourtId AND branch_id = @BranchId AND is_active AND deleted_at IS NULL FOR UPDATE;",
                new { CourtId = courtId, request.BranchId }, transaction, cancellationToken: cancellationToken));
            if (lockedCourtId is null)
            {
                return null;
            }
        }

        //var overlaps = await HasBookingOverlapAsync(connection, transaction, request, cancellationToken);
        //if (overlaps)
        //{
        //    return null;
        //}

        var customerId = knownCustomerId
            ?? await ResolveCustomerAsync(connection, transaction, request, cancellationToken);
        if (customerId is null)
        {
            return null;
        }

        var bookingId = Guid.NewGuid();
        var bookingType = BookingTypes.AllowedForBookings.Single(value => string.Equals(value, request.BookingType, StringComparison.OrdinalIgnoreCase));
        var totalAmount = detailPrices.Sum();
        const string insertBookingSql = """
            INSERT INTO booking.bookings
                (id, booking_code, customer_id, branch_id, status, booking_type, payment_method,
                 subtotal_amount, paid_amount, total_amount, notes, is_active, created_at, updated_at)
            VALUES
                (@BookingId,
                 CONCAT('BK', TO_CHAR(clock_timestamp(), 'YYMMDDHH24MISSMS'),
                     UPPER(SUBSTRING(REPLACE(gen_random_uuid()::text, '-', '') FROM 1 FOR 5))),
                 @CustomerId, @BranchId, 'confirmed', @BookingType, @PaymentMethod,
                 @TotalAmount, 0, @TotalAmount, @Notes, TRUE, NOW(), NOW());
            """;
        await connection.ExecuteAsync(new CommandDefinition(insertBookingSql, new
        {
            BookingId = bookingId,
            CustomerId = customerId.Value,
            request.BranchId,
            BookingType = bookingType,
            request.PaymentMethod,
            request.Notes,
            TotalAmount = totalAmount
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
                @Price
            );
            """;
        for (var i = 0; i < request.Details.Count; i++)
        {
            var detail = request.Details[i];
            await connection.ExecuteAsync(new CommandDefinition(insertDetailSql, new
            {
                DetailId = Guid.NewGuid(),
                BookingId = bookingId,
                detail.CourtId,
                detail.BookingDate,
                StartTime = TimeOnly.Parse(detail.StartTime),
                EndTime = TimeOnly.Parse(detail.EndTime),
                Price = detailPrices[i]
            }, transaction, cancellationToken: cancellationToken));
        }

        var response = await GetCreatedBookingAsync(connection, transaction, bookingId, cancellationToken);
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
        using var reader = await connection.QueryMultipleAsync(new CommandDefinition("""
            SELECT b.id AS BookingId,
                   b.booking_code AS BookingCode,
                   b.branch_id AS BranchId,
                   br.name AS BranchName,
                   cu.id AS CustomerId,
                   COALESCE(cu.full_name, '') AS CustomerName,
                   COALESCE(cu.phone_number, '') AS PhoneNumber,
                   UPPER(b.booking_type) AS BookingType,
                   UPPER(b.status) AS BookingStatus,
                   UPPER(b.payment_method) AS PaymentMethod,
                   CASE WHEN b.paid_amount >= b.total_amount THEN 'PAID' ELSE 'UNPAID' END AS PaymentStatus,
                   b.total_amount AS TotalAmount,
                   b.paid_amount AS PaidAmount,
                   b.notes AS Notes
            FROM booking.bookings b
            INNER JOIN core.branches br ON br.id = b.branch_id
            LEFT JOIN customer.customers cu ON cu.id = b.customer_id
            WHERE b.id = @BookingId
              AND b.is_active = TRUE
              AND b.deleted_at IS NULL;

            SELECT bd.id AS BookingDetailId,
                   bd.court_id AS CourtId,
                   COALESCE(NULLIF(c.name, ''), c.court_number::text) AS CourtName,
                   bd.booking_date AS BookingDate,
                   bd.start_time AS StartTime,
                   bd.end_time AS EndTime,
                   bd.price_charged AS Price
            FROM booking.booking_details bd
            INNER JOIN core.courts c ON c.id = bd.court_id
            WHERE bd.booking_id = @BookingId
            ORDER BY bd.booking_date ASC,
                     bd.start_time ASC,
                     CourtName ASC,
                     bd.id ASC;
            """, new { BookingId = bookingId }, cancellationToken: cancellationToken));

        var booking = await reader.ReadSingleOrDefaultAsync<BookingDetailResponse>();
        var details = (await reader.ReadAsync<BookingCourtItemResponse>()).AsList();
        if (booking is null)
        {
            return null;
        }

        booking.Details = details;
        return booking;
    }

    public async Task<BookingStatusProjection?> GetBookingStatusAsync(Guid bookingId, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<BookingStatusProjection>(new CommandDefinition("""
            SELECT
                id AS BookingId,
                booking_code AS BookingCode,
                LOWER(status) AS Status,
                updated_at AS UpdatedAt
            FROM booking.bookings
            WHERE id = @BookingId
              AND is_active = TRUE
              AND deleted_at IS NULL;
            """, new { BookingId = bookingId }, cancellationToken: cancellationToken));
    }

    public async Task<UpdateBookingStatusResponse?> UpdateBookingStatusAsync(
        Guid bookingId,
        string currentStatus,
        string newStatus,
        Guid? updatedBy,
        CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<UpdateBookingStatusResponse>(new CommandDefinition("""
            UPDATE booking.bookings
            SET status = @NewStatus,
                updated_at = NOW(),
                updated_by = @UpdatedBy
            WHERE id = @BookingId
              AND LOWER(status) = @CurrentStatus
              AND is_active = TRUE
              AND deleted_at IS NULL
            RETURNING
                id AS BookingId,
                booking_code AS BookingCode,
                UPPER(status) AS Status,
                updated_at AS UpdatedAt;
            """, new
        {
            BookingId = bookingId,
            CurrentStatus = currentStatus,
            NewStatus = newStatus,
            UpdatedBy = updatedBy
        }, cancellationToken: cancellationToken));
    }

    private async Task<Guid?> ResolveCustomerAsync(IDbConnection connection, IDbTransaction transaction, CreateBookingRequest request, CancellationToken cancellationToken)
    {
        //if (request.CustomerId is Guid customerId)
        //{
        //    return await connection.ExecuteScalarAsync<Guid?>(new CommandDefinition(
        //        "SELECT id FROM customer.customers WHERE id = @CustomerId AND is_active AND deleted_at IS NULL;",
        //        new { CustomerId = customerId }, transaction, cancellationToken: cancellationToken));
        //}

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

    private static async Task<bool> HasBookingOverlapAsync(IDbConnection connection, IDbTransaction transaction, CreateBookingDetailRequest detail, CancellationToken cancellationToken) =>
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
            """, new { detail.CourtId, detail.BookingDate, StartTime = TimeOnly.Parse(detail.StartTime), EndTime = TimeOnly.Parse(detail.EndTime) }, transaction, cancellationToken: cancellationToken));

    private static async Task<CreateBookingResponse?> GetCreatedBookingAsync(IDbConnection connection, IDbTransaction transaction, Guid bookingId, CancellationToken cancellationToken)
    {
        var booking = await connection.QuerySingleOrDefaultAsync<CreateBookingResponse>(new CommandDefinition("""
            SELECT b.id AS BookingId,
                   b.booking_code AS BookingCode,
                   b.branch_id AS BranchId,
                   cu.id AS CustomerId,
                   cu.full_name AS CustomerName,
                   cu.phone_number AS PhoneNumber,
                   UPPER(b.booking_type) AS BookingType,
                   UPPER(b.status) AS BookingStatus,
                   b.payment_method AS PaymentMethod,
                   b.total_amount AS TotalAmount,
                   b.notes AS Notes
            FROM booking.bookings b
            JOIN customer.customers cu ON cu.id = b.customer_id
            WHERE b.id = @BookingId AND b.is_active AND b.deleted_at IS NULL;
            """, new { BookingId = bookingId }, transaction, cancellationToken: cancellationToken));
        if (booking is null)
        {
            return null;
        }

        var details = await connection.QueryAsync<CreateBookingDetailResponse>(new CommandDefinition("""
            SELECT bd.court_id AS CourtId,
                   COALESCE(NULLIF(c.name, ''), c.court_number::text) AS CourtName,
                   bd.booking_date AS BookingDate,
                   bd.start_time AS StartTime,
                   bd.end_time AS EndTime,
                   bd.price_charged AS Price
            FROM booking.booking_details bd
            JOIN core.courts c ON c.id = bd.court_id
            WHERE bd.booking_id = @BookingId
            ORDER BY bd.booking_date, bd.start_time, CourtName;
            """, new { BookingId = bookingId }, transaction, cancellationToken: cancellationToken));
        booking.Details = details.AsList();
        return booking;
    }

    private async Task<T> ScalarAsync<T>(string sql, object parameters, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<T>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }
}