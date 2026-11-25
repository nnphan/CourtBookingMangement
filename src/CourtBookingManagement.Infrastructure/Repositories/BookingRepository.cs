using System.Data;
using System.Security.Cryptography;
using CourtBookingManagement.Application.Abstractions.Data;
using CourtBookingManagement.Application.Bookings.DTOs;
using CourtBookingManagement.Application.Bookings.Exceptions;
using CourtBookingManagement.Application.Bookings.Interfaces;
using CourtBookingManagement.Application.Bookings.Models;
using CourtBookingManagement.Application.Common.Enums;
using CourtBookingManagement.Domain.Bookings;
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
              AND UPPER(b.status) IN ('PENDING', 'PENDING_PAYMENT', 'CONFIRMED')
              AND (LOWER(b.status) <> 'pending_payment' OR b.expires_at > NOW())
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
                  AND UPPER(bd.status) IN ('HELD', 'CONFIRMED')
                  AND (LOWER(b.status) <> 'pending_payment' OR b.expires_at > NOW())
                  AND @StartTime < bd.end_time ::time
                  AND @EndTime > bd.start_time ::time
            );
            """, new { CourtId = courtId, BookingDate = bookingDate, StartTime = startTime, EndTime = endTime }, cancellationToken);

    public async Task<CreateBookingResponse?> CreateBookingAsync(
        CreateBookingRequest request,
        IReadOnlyList<decimal> detailPrices,
        Guid? knownCustomerId,
        int? holdDurationSeconds,
        byte[]? holdTokenHash,
        Guid? idempotencyKey,
        byte[]? idempotencyRequestHash,
        Guid? selectionSubjectId,
        CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);

        if (idempotencyKey is Guid key)
        {
            if (idempotencyRequestHash is null)
            {
                throw new InvalidOperationException("An idempotency request hash is required.");
            }

            await connection.ExecuteAsync(new CommandDefinition(
                "SELECT pg_advisory_xact_lock(hashtextextended(@LockKey, 0));",
                new { LockKey = $"booking-idempotency:{key:D}" },
                transaction,
                cancellationToken: cancellationToken));

            var existing = await connection.QuerySingleOrDefaultAsync<IdempotencyProjection>(new CommandDefinition("""
                SELECT id AS BookingId,
                       idempotency_request_hash AS RequestHash
                FROM booking.bookings
                WHERE idempotency_key = @IdempotencyKey
                FOR UPDATE;
                """, new { IdempotencyKey = key }, transaction, cancellationToken: cancellationToken));
            if (existing is not null)
            {
                if (!CryptographicOperations.FixedTimeEquals(existing.RequestHash, idempotencyRequestHash))
                {
                    transaction.Rollback();
                    throw new BookingIdempotencyKeyReusedException();
                }

                await ReplaceActiveHoldTokenHashAsync(
                    connection,
                    transaction,
                    existing.BookingId,
                    holdTokenHash,
                    cancellationToken);
                var existingResponse = await GetCreatedBookingAsync(
                    connection,
                    transaction,
                    existing.BookingId,
                    cancellationToken);
                transaction.Commit();
                SetRemainingSeconds(existingResponse);
                return existingResponse;
            }

        }

        // Lock in a stable order so concurrent multi-court requests cannot deadlock.
        var lockKeys = request.Details
            .Select(detail => FormattableString.Invariant($"{detail.CourtId:D}:{detail.BookingDate:yyyy-MM-dd}"))
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

        var branchTimeZone = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            "SELECT COALESCE(time_zone, 'Asia/Ho_Chi_Minh') FROM core.branches WHERE id = @BranchId AND is_active AND deleted_at IS NULL FOR SHARE;",
            new { request.BranchId }, transaction, cancellationToken: cancellationToken));
        if (branchTimeZone is null)
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

        if (request.SelectionHoldId is Guid selectionId)
        {
            if (selectionSubjectId is null || request.OwnerSessionId is null ||
                request.Details.Any(detail => detail.BookingDate != request.Details[0].BookingDate))
                return null;

            var selection = await connection.ExecuteScalarAsync<Guid?>(new CommandDefinition("""
                SELECT id FROM booking.court_selection_holds
                WHERE id = @selectionId AND subject_id = @selectionSubjectId AND session_id = @OwnerSessionId
                  AND branch_id = @BranchId AND business_date = @BusinessDate AND closed_at IS NULL
                FOR UPDATE
                """, new { selectionId, selectionSubjectId, request.OwnerSessionId, request.BranchId,
                             BusinessDate = request.Details[0].BookingDate },
                transaction, cancellationToken: cancellationToken));
            if (selection is null) return null;

            var requested = System.Text.Json.JsonSerializer.Serialize(request.Details);
            var converted = await connection.ExecuteAsync(new CommandDefinition("""
                WITH instant AS MATERIALIZED (SELECT clock_timestamp() AS now),
                requested AS (
                    SELECT "CourtId" AS court_id, range_agg(tstzrange(
                        ("BookingDate" + "StartTime") AT TIME ZONE @branchTimeZone,
                        ("BookingDate" + "EndTime") AT TIME ZONE @branchTimeZone, '[)')) AS spans,
                        min(("BookingDate" + "StartTime") AT TIME ZONE @branchTimeZone) AS first_start
                    FROM jsonb_to_recordset(CAST(@requested AS jsonb))
                        AS d("CourtId" uuid, "BookingDate" date, "StartTime" time, "EndTime" time)
                    GROUP BY "CourtId"
                ),
                held AS (
                    SELECT court_id, range_agg(slot_range) AS spans
                    FROM booking.court_selection_slots WHERE hold_id = @selectionId GROUP BY court_id
                )
                UPDATE booking.court_selection_holds SET closed_at = instant.now
                FROM instant WHERE id = @selectionId AND expires_at > instant.now
                  AND NOT EXISTS (
                    SELECT 1 FROM requested FULL JOIN held USING (court_id)
                    WHERE requested.spans IS DISTINCT FROM held.spans OR requested.first_start <= instant.now
                  )
                """, new { selectionId, requested, branchTimeZone }, transaction, cancellationToken: cancellationToken));
            if (converted != 1) return null;
        }

        var customerId = knownCustomerId
            ?? await ResolveCustomerAsync(connection, transaction, request, cancellationToken);
        if (customerId is null)
        {
            return null;
        }

        var bookingId = Guid.NewGuid();
        var bookingType = BookingTypes.AllowedForBookings.Single(value => string.Equals(value, request.BookingType, StringComparison.OrdinalIgnoreCase));
        var totalAmount = detailPrices.Sum();
        var bookingStatus = holdDurationSeconds is null
            ? BookingStatuses.Confirmed
            : BookingStatuses.PendingPayment;
        const string insertBookingSql = """
            INSERT INTO booking.bookings
                (id, booking_code, customer_id, branch_id, status, booking_type, payment_method,
                 subtotal_amount, paid_amount, total_amount, expires_at, confirmed_at, notes,
                 hold_token_hash, idempotency_key, idempotency_request_hash,
                 is_active, created_at, updated_at)
            VALUES
                (@BookingId,
                 CONCAT('BK', TO_CHAR(clock_timestamp(), 'YYMMDDHH24MISSMS'),
                     UPPER(SUBSTRING(REPLACE(gen_random_uuid()::text, '-', '') FROM 1 FOR 5))),
                 @CustomerId, @BranchId, @BookingStatus, @BookingType, @PaymentMethod,
                 @TotalAmount, 0, @TotalAmount,
                 CASE WHEN @HoldDurationSeconds IS NULL THEN NULL
                      ELSE clock_timestamp() + make_interval(secs => @HoldDurationSeconds) END,
                 CASE WHEN @HoldDurationSeconds IS NULL THEN clock_timestamp() ELSE NULL END,
                 @Notes, @HoldTokenHash, @IdempotencyKey, @IdempotencyRequestHash,
                 TRUE, NOW(), NOW());
            """;
        await connection.ExecuteAsync(new CommandDefinition(insertBookingSql, new
        {
            BookingId = bookingId,
            CustomerId = customerId.Value,
            request.BranchId,
            BookingStatus = bookingStatus,
            BookingType = bookingType,
            request.PaymentMethod,
            request.Notes,
            TotalAmount = totalAmount,
            HoldDurationSeconds = holdDurationSeconds,
            HoldTokenHash = holdTokenHash,
            IdempotencyKey = idempotencyKey,
            IdempotencyRequestHash = idempotencyRequestHash
        }, transaction, cancellationToken: cancellationToken));

        if (request.SelectionHoldId is Guid convertedHoldId)
            await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE booking.court_selection_holds SET booking_id = @bookingId WHERE id = @convertedHoldId
                """, new { bookingId, convertedHoldId }, transaction, cancellationToken: cancellationToken));

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
                tstzrange(
                    (@BookingDate + @StartTime) AT TIME ZONE @TimeZone,
                    (@BookingDate + @EndTime) AT TIME ZONE @TimeZone,
                    '[)'
                ),
                @DetailStatus,
                @Price
            );
            """;
        try
        {
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
                    TimeZone = branchTimeZone,
                    DetailStatus = holdDurationSeconds is null
                        ? BookingDetailStatuses.Confirmed
                        : BookingDetailStatuses.Held,
                    Price = detailPrices[i]
                }, transaction, cancellationToken: cancellationToken));
            }
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.ExclusionViolation)
        {
            transaction.Rollback();
            return null;
        }

        var response = await GetCreatedBookingAsync(connection, transaction, bookingId, cancellationToken);
        if (response is null)
        {
            return null;
        }

        transaction.Commit();
        SetRemainingSeconds(response);

        return response;
    }

    public async Task<IdempotentBookingLookup?> GetIdempotentBookingAsync(
        Guid idempotencyKey,
        byte[] requestHash,
        byte[]? replacementHoldTokenHash,
        CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);
        await connection.ExecuteAsync(new CommandDefinition(
            "SELECT pg_advisory_xact_lock(hashtextextended(@LockKey, 0));",
            new { LockKey = $"booking-idempotency:{idempotencyKey:D}" },
            transaction,
            cancellationToken: cancellationToken));

        var existing = await connection.QuerySingleOrDefaultAsync<IdempotencyProjection>(new CommandDefinition("""
            SELECT id AS BookingId,
                   idempotency_request_hash AS RequestHash
            FROM booking.bookings
            WHERE idempotency_key = @IdempotencyKey
            FOR UPDATE;
            """, new { IdempotencyKey = idempotencyKey }, transaction, cancellationToken: cancellationToken));
        if (existing is null)
        {
            transaction.Commit();
            return null;
        }

        if (!CryptographicOperations.FixedTimeEquals(existing.RequestHash, requestHash))
        {
            transaction.Commit();
            return new IdempotentBookingLookup(true, null);
        }

        await ReplaceActiveHoldTokenHashAsync(
            connection,
            transaction,
            existing.BookingId,
            replacementHoldTokenHash,
            cancellationToken);
        var response = await GetCreatedBookingAsync(
            connection,
            transaction,
            existing.BookingId,
            cancellationToken);
        transaction.Commit();
        SetRemainingSeconds(response);
        return new IdempotentBookingLookup(false, response);
    }

    public async Task<BookingDetailResponse?> GetBookingHoldAsync(
        Guid bookingId,
        Guid? customerId,
        byte[]? holdTokenHash,
        CancellationToken cancellationToken)
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
                   b.expires_at AS ExpiresAt,
                   clock_timestamp() AS ServerTime,
                   b.notes AS Notes
            FROM booking.bookings b
            INNER JOIN core.branches br ON br.id = b.branch_id
            LEFT JOIN customer.customers cu ON cu.id = b.customer_id
            WHERE b.id = @BookingId
              AND b.is_active = TRUE
              AND b.deleted_at IS NULL
              AND (b.customer_id = @CustomerId OR
                   (@HoldTokenHash IS NOT NULL AND b.hold_token_hash = @HoldTokenHash));

            SELECT bd.id AS BookingDetailId,
                   bd.court_id AS CourtId,
                   COALESCE(NULLIF(c.name, ''), c.court_number::text) AS CourtName,
                   bd.booking_date AS BookingDate,
                   bd.start_time AS StartTime,
                   bd.end_time AS EndTime,
                   bd.price_charged AS Price,
                   UPPER(bd.status) AS Status
            FROM booking.booking_details bd
            INNER JOIN core.courts c ON c.id = bd.court_id
            WHERE bd.booking_id = @BookingId
            ORDER BY bd.booking_date, bd.start_time, CourtName, bd.id;
            """, new { BookingId = bookingId, CustomerId = customerId, HoldTokenHash = holdTokenHash }, cancellationToken: cancellationToken));

        var booking = await reader.ReadSingleOrDefaultAsync<BookingDetailResponse>();
        var details = (await reader.ReadAsync<BookingCourtItemResponse>()).AsList();
        if (booking is null)
        {
            return null;
        }

        booking.Details = details;
        if (booking.ExpiresAt is DateTimeOffset expiresAt)
        {
            booking.RemainingSeconds = Math.Max(0, (int)Math.Ceiling((expiresAt - booking.ServerTime).TotalSeconds));
        }

        return booking;
    }

    private static Task ReplaceActiveHoldTokenHashAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        Guid bookingId,
        byte[]? replacementHash,
        CancellationToken cancellationToken)
    {
        if (replacementHash is null)
        {
            return Task.CompletedTask;
        }

        return connection.ExecuteAsync(new CommandDefinition("""
            UPDATE booking.bookings
            SET hold_token_hash = @ReplacementHash,
                updated_at = clock_timestamp()
            WHERE id = @BookingId
              AND LOWER(status) = 'pending_payment'
              AND expires_at > clock_timestamp();
            """, new { BookingId = bookingId, ReplacementHash = replacementHash },
            transaction, cancellationToken: cancellationToken));
    }

    private static void SetRemainingSeconds(CreateBookingResponse? response)
    {
        if (response?.ExpiresAt is DateTimeOffset expiresAt)
        {
            response.RemainingSeconds = Math.Max(
                0,
                (int)Math.Ceiling((expiresAt - response.ServerTime).TotalSeconds));
        }
    }

    public async Task<ExpiredBookingHold?> CancelBookingHoldAsync(
        Guid bookingId,
        Guid? customerId,
        byte[]? holdTokenHash,
        CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);

        var lockedBookingId = await connection.ExecuteScalarAsync<Guid?>(new CommandDefinition("""
            SELECT id
            FROM booking.bookings
            WHERE id = @BookingId
              AND LOWER(status) = 'pending_payment'
              AND expires_at > NOW()
              AND is_active = TRUE
              AND deleted_at IS NULL
              AND (customer_id = @CustomerId OR
                   (@HoldTokenHash IS NOT NULL AND hold_token_hash = @HoldTokenHash))
            FOR UPDATE;
            """, new { BookingId = bookingId, CustomerId = customerId, HoldTokenHash = holdTokenHash },
            transaction, cancellationToken: cancellationToken));
        if (lockedBookingId is null)
        {
            transaction.Commit();
            return null;
        }

        var slots = await connection.QueryAsync<BookingHoldSlot>(new CommandDefinition("""
            SELECT court_id AS CourtId,
                   booking_date AS BookingDate,
                   start_time AS StartTime,
                   end_time AS EndTime
            FROM booking.booking_details
            WHERE booking_id = @BookingId
              AND LOWER(status) = 'held';
            """, new { BookingId = bookingId }, transaction, cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE booking.bookings
            SET status = 'cancelled',
                expires_at = NULL,
                updated_at = NOW()
            WHERE id = @BookingId
              AND LOWER(status) = 'pending_payment'
              AND expires_at > NOW();

            UPDATE booking.booking_details
            SET status = 'cancelled'
            WHERE booking_id = @BookingId
              AND LOWER(status) = 'held';

            UPDATE payment.payment_transactions
            SET status = 'EXPIRED',
                expired_at = COALESCE(expired_at, NOW()),
                updated_at = NOW()
            WHERE booking_id = @BookingId
              AND status IN ('PENDING', 'PROCESSING');
            """, new { BookingId = bookingId }, transaction, cancellationToken: cancellationToken));
        transaction.Commit();

        return new ExpiredBookingHold(
            bookingId,
            slots.Select(slot => new BookingSlotResource(
                slot.CourtId,
                slot.BookingDate,
                slot.StartTime,
                slot.EndTime)).ToArray());
    }

    private sealed class BookingHoldSlot
    {
        public Guid CourtId { get; init; }
        public DateOnly BookingDate { get; init; }
        public TimeOnly StartTime { get; init; }
        public TimeOnly EndTime { get; init; }
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
                   UPPER(b.status) AS Status,
                   b.payment_method AS PaymentMethod,
                   b.total_amount AS TotalAmount,
                   b.notes AS Notes,
                   b.expires_at AS ExpiresAt,
                   clock_timestamp() AS ServerTime
            FROM booking.bookings b
            JOIN customer.customers cu ON cu.id = b.customer_id
            WHERE b.id = @BookingId AND b.is_active AND b.deleted_at IS NULL;
            """, new { BookingId = bookingId }, transaction, cancellationToken: cancellationToken));
        if (booking is null)
        {
            return null;
        }

        var details = await connection.QueryAsync<CreateBookingDetailResponse>(new CommandDefinition("""
            SELECT bd.id AS BookingDetailId,
                   bd.court_id AS CourtId,
                   COALESCE(NULLIF(c.name, ''), c.court_number::text) AS CourtName,
                   bd.booking_date AS BookingDate,
                   bd.start_time AS StartTime,
                   bd.end_time AS EndTime,
                   bd.price_charged AS Price,
                   UPPER(bd.status) AS Status
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

    private sealed class IdempotencyProjection
    {
        public Guid BookingId { get; init; }
        public byte[] RequestHash { get; init; } = [];
    }
}
