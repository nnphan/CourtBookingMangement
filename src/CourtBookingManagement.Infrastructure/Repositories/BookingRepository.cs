using System.Data;
using System.Data.Common;
using System.Text.Json;
using CourtBookingManagement.Application.Abstractions.Data;
using CourtBookingManagement.Application.Bookings.DTOs;
using CourtBookingManagement.Application.Bookings.Interfaces;
using CourtBookingManagement.Application.Bookings.Services;
using Dapper;
using Npgsql;

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

    public async Task<bool> CustomerExistsAsync(Guid customerId, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition("""
            SELECT EXISTS (
                SELECT 1
                FROM customer.customers
                WHERE id = @CustomerId AND is_active = TRUE AND deleted_at IS NULL
            );
            """, new { CustomerId = customerId }, cancellationToken: cancellationToken));
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

    public async Task<bool> ExistsBranchAsync(Guid branchId, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition("""
            SELECT EXISTS (
                SELECT 1
                FROM core.branches
                WHERE id = @BranchId AND is_active = TRUE AND deleted_at IS NULL
            );
            """, new { BranchId = branchId }, cancellationToken: cancellationToken));
    }

    public async Task<BookingBranchData?> GetBranchBookingDataAsync(
        Guid branchId,
        CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        var branch = await connection.QuerySingleOrDefaultAsync<BookingBranchRow>(new CommandDefinition("""
            SELECT id AS Id,
                   is_active AS IsActive,
                   deleted_at IS NOT NULL AS IsDeleted,
                   (NOW() AT TIME ZONE time_zone)::date AS BusinessDate,
                   time_zone AS TimeZone
            FROM core.branches
            WHERE id = @BranchId;
            """, new { BranchId = branchId }, cancellationToken: cancellationToken));
        if (branch is null)
        {
            return null;
        }

        var operatingHours = (await connection.QueryAsync<BookingOperatingHourData>(new CommandDefinition("""
            SELECT open_time AS OpenTime, close_time AS CloseTime, is_closed AS IsClosed
            FROM core.operating_hours
            WHERE branch_id = @BranchId;
            """, new { BranchId = branchId }, cancellationToken: cancellationToken))).AsList();

        return new BookingBranchData(
            branch.Id,
            branch.IsActive,
            branch.IsDeleted,
            branch.BusinessDate,
            branch.TimeZone,
            operatingHours);
    }

    public async Task<IReadOnlyList<BookingCourtData>> GetCourtsAsync(
        IReadOnlyCollection<Guid> courtIds,
        CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        var courts = await connection.QueryAsync<BookingCourtData>(new CommandDefinition("""
            SELECT id AS Id,
                   branch_id AS BranchId,
                   COALESCE(NULLIF(name, ''), court_number::text) AS CourtName,
                   status AS Status,
                   is_active AS IsActive,
                   deleted_at IS NOT NULL AS IsDeleted
            FROM core.courts
            WHERE id = ANY(@CourtIds);
            """, new { CourtIds = courtIds.ToArray() }, cancellationToken: cancellationToken));

        return courts.AsList();
    }

    public async Task<bool> HasBookingConflictsAsync(
        IReadOnlyCollection<CreateBookingDetailRequest> details,
        CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        return await HasBookingConflictsAsync(connection, null, details, cancellationToken);
    }

    public async Task<BookingResponse?> CreateBookingAsync(
        CreateBookingRequest request,
        Guid? customerId,
        string? guestName,
        string? guestPhoneNumber,
        Guid? actorId,
        bool requireCustomerOwnership,
        string bookingType,
        string paymentMethod,
        IReadOnlyList<decimal> detailPrices,
        CancellationToken cancellationToken)
    {
        if (request.Details is null || request.Details.Count != detailPrices.Count)
        {
            throw new InvalidOperationException("Booking details and calculated prices must have matching counts.");
        }

        using var connection = connectionFactory.CreateConnection();
        if (connection is not DbConnection dbConnection)
        {
            throw new InvalidOperationException("The booking connection must support asynchronous transactions.");
        }

        await dbConnection.OpenAsync(cancellationToken);
        await using var transaction = await dbConnection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var distinctCourtIds = request.Details.Select(detail => detail.CourtId).Distinct().ToArray();

        var branchIsAvailable = await dbConnection.ExecuteScalarAsync<bool>(new CommandDefinition("""
            SELECT EXISTS (
                SELECT 1
                FROM core.branches
                WHERE id = @BranchId AND is_active AND deleted_at IS NULL
            );
            """, new { request.BranchId }, transaction, cancellationToken: cancellationToken));
        if (!branchIsAvailable)
        {
            return null;
        }

        var lockedCourtIds = (await dbConnection.QueryAsync<Guid>(new CommandDefinition("""
            SELECT id
            FROM core.courts
            WHERE id = ANY(@CourtIds)
              AND branch_id = @BranchId
              AND is_active = TRUE
              AND deleted_at IS NULL
              AND LOWER(status) = 'active'
            ORDER BY id
            FOR UPDATE;
            """, new { CourtIds = distinctCourtIds, request.BranchId }, transaction, cancellationToken: cancellationToken))).AsList();
        if (lockedCourtIds.Count != distinctCourtIds.Length)
        {
            return null;
        }

        if (await HasBookingConflictsAsync(dbConnection, transaction, request.Details, cancellationToken))
        {
            return null;
        }

        var resolvedCustomerId = await ResolveCustomerAsync(
            dbConnection,
            transaction,
            customerId,
            guestName,
            guestPhoneNumber,
            actorId,
            requireCustomerOwnership,
            cancellationToken);
        if (resolvedCustomerId is null)
        {
            return null;
        }

        var bookingId = Guid.NewGuid();
        var subtotalAmount = detailPrices.Sum();
        var bookingCode = await dbConnection.ExecuteScalarAsync<string>(new CommandDefinition("""
            INSERT INTO booking.bookings
                (id, booking_code, customer_id, branch_id, status, booking_type, payment_method,
                 subtotal_amount, paid_amount, total_amount, notes, is_active, created_at, created_by,
                 updated_at, updated_by)
            VALUES
                (@BookingId,
                 CONCAT('BK', TO_CHAR(clock_timestamp(), 'YYMMDDHH24MISSMS'),
                     UPPER(SUBSTRING(REPLACE(gen_random_uuid()::text, '-', '') FROM 1 FOR 5))),
                 @CustomerId, @BranchId, 'confirmed', @BookingType, @PaymentMethod,
                 @SubtotalAmount, 0, @SubtotalAmount, @Notes, TRUE, NOW(), @ActorId, NOW(), @ActorId)
            RETURNING booking_code;
            """, new
        {
            BookingId = bookingId,
            CustomerId = resolvedCustomerId.Value,
            request.BranchId,
            BookingType = bookingType,
            PaymentMethod = paymentMethod,
            SubtotalAmount = subtotalAmount,
            request.Notes,
            ActorId = actorId
        }, transaction, cancellationToken: cancellationToken))
            ?? throw new InvalidOperationException("The booking code was not returned after insert.");

        var insertDetailParameters = request.Details.Select((detail, index) => new
        {
            DetailId = Guid.NewGuid(),
            BookingId = bookingId,
            detail.CourtId,
            detail.BookingDate,
            detail.StartTime,
            detail.EndTime,
            Status = "reserved",
            PriceCharged = detailPrices[index]
        }).ToArray();

        try
        {
            await dbConnection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO booking.booking_details
                    (id, booking_id, court_id, booking_date, start_time, end_time, slot_range, status, price_charged)
                VALUES
                    (@DetailId, @BookingId, @CourtId, @BookingDate, @StartTime, @EndTime, NULL, @Status, @PriceCharged);
                """, insertDetailParameters, transaction, cancellationToken: cancellationToken));
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.ExclusionViolation)
        {
            await transaction.RollbackAsync();
            throw new BookingConflictException();
        }

        var response = await GetBookingResponseAsync(dbConnection, transaction, bookingId, cancellationToken);
        if (response is null)
        {
            return null;
        }

        await transaction.CommitAsync(cancellationToken);
        return response with { BookingCode = bookingCode };
    }

    private static async Task<Guid?> ResolveCustomerAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        Guid? customerId,
        string? guestName,
        string? guestPhoneNumber,
        Guid? actorId,
        bool requireCustomerOwnership,
        CancellationToken cancellationToken)
    {
        if (customerId is Guid requestedCustomerId)
        {
            return await connection.ExecuteScalarAsync<Guid?>(new CommandDefinition("""
                SELECT id
                FROM customer.customers
                WHERE id = @CustomerId AND is_active = TRUE AND deleted_at IS NULL
                  AND (NOT @RequireCustomerOwnership OR user_id = @ActorId)
                FOR UPDATE;
                """, new { CustomerId = requestedCustomerId, RequireCustomerOwnership = requireCustomerOwnership, ActorId = actorId }, transaction, cancellationToken: cancellationToken));
        }

        if (string.IsNullOrWhiteSpace(guestName) || string.IsNullOrWhiteSpace(guestPhoneNumber))
        {
            return null;
        }

        await connection.ExecuteAsync(new CommandDefinition(
            "SELECT pg_advisory_xact_lock(hashtextextended(@LockKey, 0));",
            new { LockKey = $"customer-phone:{guestPhoneNumber}" },
            transaction,
            cancellationToken: cancellationToken));

        var existingGuestId = await connection.ExecuteScalarAsync<Guid?>(new CommandDefinition("""
            SELECT id
            FROM customer.customers
            WHERE phone_number = @PhoneNumber
              AND is_guest = TRUE
              AND is_active = TRUE
              AND deleted_at IS NULL
            ORDER BY created_at
            LIMIT 1
            FOR UPDATE;
            """, new { PhoneNumber = guestPhoneNumber }, transaction, cancellationToken: cancellationToken));
        if (existingGuestId is not null)
        {
            return existingGuestId;
        }

        var membershipLevelId = await connection.ExecuteScalarAsync<Guid?>(new CommandDefinition("""
            SELECT id
            FROM customer.membership_levels
            ORDER BY sort_order ASC, min_points ASC, id ASC
            LIMIT 1;
            """, transaction: transaction, cancellationToken: cancellationToken));

        return await connection.ExecuteScalarAsync<Guid>(new CommandDefinition("""
            INSERT INTO customer.customers
                (id, membership_level_id, full_name, phone_number, is_guest,
                 loyalty_points_balance, is_active, created_at, created_by, updated_at, updated_by)
            VALUES
                (@CustomerId, @MembershipLevelId, @CustomerName, @PhoneNumber, TRUE, 0,
                 TRUE, NOW(), @ActorId, NOW(), @ActorId)
            RETURNING id;
            """, new
        {
            CustomerId = Guid.NewGuid(),
            MembershipLevelId = membershipLevelId,
            CustomerName = guestName,
            PhoneNumber = guestPhoneNumber,
            ActorId = actorId
        }, transaction, cancellationToken: cancellationToken));
    }

    private static async Task<bool> HasBookingConflictsAsync(
        IDbConnection connection,
        IDbTransaction? transaction,
        IReadOnlyCollection<CreateBookingDetailRequest> details,
        CancellationToken cancellationToken)
    {
        var detailsJson = JsonSerializer.Serialize(details.Select(detail => new
        {
            court_id = detail.CourtId,
            booking_date = detail.BookingDate.ToString("yyyy-MM-dd"),
            start_time = detail.StartTime.ToString("HH:mm:ss"),
            end_time = detail.EndTime.ToString("HH:mm:ss")
        }));

        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition("""
            WITH requested AS (
                SELECT court_id, booking_date, start_time, end_time
                FROM jsonb_to_recordset(CAST(@DetailsJson AS jsonb))
                    AS item(court_id uuid, booking_date date, start_time time, end_time time)
            )
            SELECT EXISTS (
                SELECT 1
                FROM requested r
                JOIN booking.booking_details bd
                  ON bd.court_id = r.court_id
                 AND bd.booking_date = r.booking_date
                 AND r.start_time < bd.end_time
                 AND r.end_time > bd.start_time
                JOIN booking.bookings b ON b.id = bd.booking_id
                WHERE LOWER(bd.status) IN ('reserved', 'confirmed')
                  AND b.is_active = TRUE
                  AND b.deleted_at IS NULL
                  AND LOWER(b.status) NOT IN ('cancelled', 'expired', 'completed')
            );
            """, new { DetailsJson = detailsJson }, transaction, cancellationToken: cancellationToken));
    }

    private static async Task<BookingResponse?> GetBookingResponseAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        Guid bookingId,
        CancellationToken cancellationToken)
    {
        var header = await connection.QuerySingleOrDefaultAsync<BookingHeaderRow>(new CommandDefinition("""
            SELECT id AS Id,
                   booking_code AS BookingCode,
                   customer_id AS CustomerId,
                   branch_id AS BranchId,
                   status AS Status,
                   booking_type AS BookingType,
                   subtotal_amount AS SubtotalAmount,
                   paid_amount AS PaidAmount,
                   total_amount AS TotalAmount,
                   payment_method AS PaymentMethod,
                   notes AS Notes
            FROM booking.bookings
            WHERE id = @BookingId AND is_active = TRUE AND deleted_at IS NULL;
            """, new { BookingId = bookingId }, transaction, cancellationToken: cancellationToken));
        if (header is null)
        {
            return null;
        }

        var details = (await connection.QueryAsync<BookingReservationResponse>(new CommandDefinition("""
            SELECT bd.id AS Id,
                   bd.court_id AS CourtId,
                   COALESCE(NULLIF(c.name, ''), c.court_number::text) AS CourtName,
                   bd.booking_date AS BookingDate,
                   bd.start_time AS StartTime,
                   bd.end_time AS EndTime,
                   bd.status AS Status,
                   bd.price_charged AS PriceCharged
            FROM booking.booking_details bd
            JOIN core.courts c ON c.id = bd.court_id
            WHERE bd.booking_id = @BookingId
            ORDER BY bd.booking_date, bd.start_time, bd.court_id;
            """, new { BookingId = bookingId }, transaction, cancellationToken: cancellationToken))).AsList();

        return new BookingResponse
        {
            Id = header.Id,
            BookingCode = header.BookingCode,
            CustomerId = header.CustomerId,
            BranchId = header.BranchId,
            Status = header.Status,
            BookingType = header.BookingType,
            SubtotalAmount = header.SubtotalAmount,
            PaidAmount = header.PaidAmount,
            TotalAmount = header.TotalAmount,
            PaymentMethod = header.PaymentMethod,
            Notes = header.Notes,
            CourtCount = details.Select(detail => detail.CourtId).Distinct().Count(),
            Details = details
        };
    }

    private sealed class BookingBranchRow
    {
        public Guid Id { get; init; }
        public bool IsActive { get; init; }
        public bool IsDeleted { get; init; }
        public DateOnly BusinessDate { get; init; }
        public string TimeZone { get; init; } = string.Empty;
    }

    private sealed class BookingHeaderRow
    {
        public Guid Id { get; init; }
        public string BookingCode { get; init; } = string.Empty;
        public Guid CustomerId { get; init; }
        public Guid BranchId { get; init; }
        public string Status { get; init; } = string.Empty;
        public string BookingType { get; init; } = string.Empty;
        public decimal SubtotalAmount { get; init; }
        public decimal PaidAmount { get; init; }
        public decimal TotalAmount { get; init; }
        public string PaymentMethod { get; init; } = string.Empty;
        public string? Notes { get; init; }
    }
}
