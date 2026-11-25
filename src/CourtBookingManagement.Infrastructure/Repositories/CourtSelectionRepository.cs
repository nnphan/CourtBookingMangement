using System.Data;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text.Json;
using CourtBookingManagement.Application.Abstractions.Data;
using CourtBookingManagement.Application.Bookings.DTOs;
using Dapper;
using Microsoft.Extensions.Options;

namespace CourtBookingManagement.Infrastructure.Repositories;

public sealed class CourtSelectionRepository(ISqlConnectionFactory factory, IOptions<CourtSelectionOptions> options)
{
    private const string DatabaseTime = "SELECT clock_timestamp()";

    public async Task<bool> IsPublicBranchAsync(Guid branchId, CancellationToken ct)
    {
        using var connection = factory.CreateConnection();
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition("""
            SELECT EXISTS (SELECT 1 FROM core.branches WHERE id = @branchId AND is_active AND deleted_at IS NULL)
            """, new { branchId }, cancellationToken: ct));
    }

    public async Task<CourtSelectionBoard> ReadAsync(Guid branch, DateOnly date, Guid? subject, Guid? session, CancellationToken ct)
    {
        using var connection = factory.CreateConnection();
        using var rows = await connection.QueryMultipleAsync(new CommandDefinition("""
            SELECT clock_timestamp() AS "ServerTime", (
                SELECT coalesce(time_zone, 'Asia/Ho_Chi_Minh') FROM core.branches
                WHERE id = @branch AND is_active AND deleted_at IS NULL
            ) AS "TimeZone";
            SELECT open_time::text AS "StartTime", close_time::text AS "EndTime"
            FROM core.operating_hours WHERE branch_id = @branch AND NOT is_closed;
            WITH instant AS MATERIALIZED (SELECT clock_timestamp() AS now)
            SELECT s.id AS "SlotId", s.court_id AS "CourtId",
                   to_char(lower(s.slot_range) AT TIME ZONE coalesce(b.time_zone, 'Asia/Ho_Chi_Minh'), 'HH24:MI:SS') AS "StartTime",
                   to_char(upper(s.slot_range) AT TIME ZONE coalesce(b.time_zone, 'Asia/Ho_Chi_Minh'), 'HH24:MI:SS') AS "EndTime",
                   lower(s.slot_range) AS "StartAt", upper(s.slot_range) AS "EndAt",
                   h.expires_at AS "ExpiresAt",
                   CASE WHEN h.subject_id = @subject AND h.session_id = @session THEN h.id END AS "HoldId"
            FROM booking.court_selection_holds h
            JOIN booking.court_selection_slots s ON s.hold_id = h.id
            JOIN core.branches b ON b.id = h.branch_id
            CROSS JOIN instant
            WHERE h.branch_id = @branch AND h.business_date = @date
              AND b.is_active AND b.deleted_at IS NULL AND h.closed_at IS NULL AND h.expires_at > instant.now;
            """, new { branch, date, subject, session }, cancellationToken: ct));
        var metadata = await rows.ReadSingleAsync<BoardMetadata>();
        var windows = (await rows.ReadAsync<CourtSelectionWindow>()).AsList();
        var slots = (await rows.ReadAsync<CourtSelectionSlot>()).AsList();
        return new(branch, date, metadata.ServerTime, metadata.TimeZone, windows, slots);
    }

    public async Task<CourtSelectionResult> MutateAsync(
        CourtSelectionOwner owner, Guid key, CourtSelectionRequest request, bool release, CancellationToken ct)
    {
        await using var connection = (DbConnection)factory.CreateConnection();
        await connection.OpenAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        var subject = owner.SubjectId;
        await LockAsync(connection, transaction, $"court-selection-owner:{subject:D}", ct);
        var hash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { request, release })));
        CommandDefinition Command(string sql, object? args = null) => new(sql, args, transaction, cancellationToken: ct);
        var replay = await connection.QuerySingleOrDefaultAsync<StoredOperation>(Command("""
            SELECT request_hash AS "RequestHash", response::text AS "Response"
            FROM booking.court_selection_operations WHERE subject_id = @subject AND idempotency_key = @key
            """, new { subject, key }));
        if (replay is not null)
            return replay.RequestHash == hash
                ? JsonSerializer.Deserialize<CourtSelectionResult>(replay.Response)!
                : new("IDEMPOTENCY_KEY_REUSED");

        if (!release)
            await LockAsync(connection, transaction, FormattableString.Invariant($"{request.CourtId:D}:{request.BusinessDate:yyyy-MM-dd}"), ct);
        var result = release ? await ReleaseAsync() : await ClaimAsync();
        await connection.ExecuteAsync(Command("""
            INSERT INTO booking.court_selection_operations(subject_id, idempotency_key, request_hash, response, retain_until)
            VALUES (@subject, @key, @hash, CAST(@response AS jsonb), clock_timestamp() + make_interval(mins => @retention))
            """, new { subject, key, hash, response = JsonSerializer.Serialize(result),
                         retention = options.Value.IdempotencyRetentionMinutes }));
        await transaction.CommitAsync(ct);
        return result;

        async Task<CourtSelectionResult> ClaimAsync()
        {
            var zone = await connection.ExecuteScalarAsync<string?>(Command("""
                SELECT coalesce(time_zone, 'Asia/Ho_Chi_Minh') FROM core.branches
                WHERE id = @BranchId AND is_active AND deleted_at IS NULL FOR SHARE
                """, request));
            if (zone is null) return new("SLOT_NOT_FOUND");
            var status = await connection.ExecuteScalarAsync<string?>(Command("""
                SELECT lower(status) FROM core.courts
                WHERE id = @CourtId AND branch_id = @BranchId AND is_active AND deleted_at IS NULL FOR SHARE
                """, request));
            if (status is null) return new("SLOT_NOT_FOUND");
            if (status != "active") return new("SLOT_BLOCKED");

            var holds = (await connection.QueryAsync<Hold>(Command("""
                SELECT id AS "Id", branch_id AS "BranchId", business_date AS "BusinessDate",
                       session_id AS "SessionId", expires_at AS "ExpiresAt"
                FROM booking.court_selection_holds
                WHERE subject_id = @subject AND closed_at IS NULL ORDER BY id FOR UPDATE
                """, new { subject }))).AsList();
            var now = await connection.ExecuteScalarAsync<DateTime>(Command(DatabaseTime));
            var slot = await connection.QuerySingleAsync<SlotCheck>(Command("""
                WITH requested AS (
                    SELECT (@BusinessDate + @StartTime) AT TIME ZONE @zone AS start_at,
                           (@BusinessDate + @EndTime) AT TIME ZONE @zone AS end_at
                )
                SELECT start_at AS "StartAt", end_at AS "EndAt", start_at <= @now AS "Past",
                       EXISTS (SELECT 1 FROM core.operating_hours WHERE branch_id = @BranchId
                               AND NOT is_closed AND @StartTime >= open_time AND @EndTime <= close_time) AS "Open",
                       EXISTS (
                           SELECT 1 FROM booking.booking_details d
                           JOIN booking.bookings b ON b.id = d.booking_id
                           WHERE d.court_id = @CourtId
                             AND (upper(d.status) IN ('HELD', 'CONFIRMED') OR
                                  (upper(d.status) = 'RESERVED' AND b.is_active AND b.deleted_at IS NULL))
                             AND d.slot_range && CASE WHEN start_at < end_at THEN tstzrange(start_at, end_at, '[)') END
                       ) AS "Booked"
                FROM requested
                """, new { request.BranchId, request.BusinessDate, request.CourtId, request.StartTime, request.EndTime, zone, now }));
            if (slot.EndAt <= slot.StartAt) return new("INVALID_SLOT_SELECTION");
            if (slot.Past) return new("SLOT_IN_PAST");
            if (!slot.Open) return new("SLOT_BLOCKED");
            if (slot.Booked) return new("SLOT_ALREADY_BOOKED");

            await connection.ExecuteAsync(Command("""
                UPDATE booking.court_selection_holds SET closed_at = @now
                WHERE subject_id = @subject AND closed_at IS NULL AND expires_at <= @now
                """, new { subject, now }));
            var active = holds.Where(h => h.ExpiresAt > now).ToList();
            var hold = active.Find(h => h.SessionId == request.OwnerSessionId);
            if (request.HoldId is Guid expected && hold?.Id != expected)
            {
                var mismatch = await connection.QuerySingleOrDefaultAsync<string>(Command("""
                    SELECT CASE WHEN subject_id <> @subject OR session_id <> @OwnerSessionId THEN 'HOLD_OWNERSHIP_MISMATCH'
                                WHEN expires_at <= @now THEN 'HOLD_EXPIRED' ELSE 'HOLD_NOT_FOUND' END
                    FROM booking.court_selection_holds WHERE id = @expected
                    """, new { expected, subject, request.OwnerSessionId, now }));
                return new(mismatch ?? "HOLD_NOT_FOUND");
            }
            if (hold is not null && (hold.BranchId != request.BranchId || hold.BusinessDate != request.BusinessDate))
                return new("INVALID_SLOT_SELECTION");
            var overlap = await connection.QueryFirstOrDefaultAsync<Overlap>(Command("""
                WITH requested AS (
                    SELECT tstzrange((@BusinessDate + @StartTime) AT TIME ZONE @zone,
                                     (@BusinessDate + @EndTime) AT TIME ZONE @zone, '[)') AS span
                )
                SELECT s.id AS "SlotId", s.hold_id AS "HoldId", s.slot_range = span AS "Exact"
                FROM booking.court_selection_slots s JOIN booking.court_selection_holds h ON h.id = s.hold_id
                CROSS JOIN requested
                WHERE s.court_id = @CourtId AND s.slot_range && span
                  AND h.closed_at IS NULL AND h.expires_at > @now
                """, new { request.BusinessDate, request.CourtId, request.StartTime, request.EndTime, zone, now }));
            if (overlap is not null)
                return hold is not null && overlap.HoldId == hold.Id && overlap.Exact
                    ? ClaimResult(overlap.HoldId, overlap.SlotId, hold.ExpiresAt)
                    : new("SLOT_NOT_AVAILABLE");

            if (hold is null && active.Count >= options.Value.MaximumActiveHoldsPerUser)
                return new("HOLD_LIMIT_EXCEEDED");

            var holdId = hold?.Id ?? Guid.NewGuid();
            var expiresAt = hold?.ExpiresAt ?? now.AddMinutes(options.Value.SelectionHoldMinutes);
            var count = await connection.ExecuteScalarAsync<int>(Command("""
                SELECT count(*)::integer FROM booking.court_selection_slots WHERE hold_id = @holdId
                """, new { holdId }));
            if (count >= options.Value.MaximumSlotsPerHold) return new("HOLD_SLOT_LIMIT_EXCEEDED");
            if (hold is null)
                await connection.ExecuteAsync(Command("""
                    INSERT INTO booking.court_selection_holds(id, branch_id, business_date, subject_id, user_id, session_id, expires_at)
                    VALUES (@holdId, @BranchId, @BusinessDate, @subject, @UserId, @OwnerSessionId, @expiresAt)
                    """, new { holdId, request.BranchId, request.BusinessDate, subject, owner.UserId, request.OwnerSessionId, expiresAt }));
            var slotId = Guid.NewGuid();
            await connection.ExecuteAsync(Command("""
                INSERT INTO booking.court_selection_slots(id, hold_id, court_id, slot_range)
                VALUES (@slotId, @holdId, @CourtId, tstzrange(
                    (@BusinessDate + @StartTime) AT TIME ZONE @zone,
                    (@BusinessDate + @EndTime) AT TIME ZONE @zone, '[)'))
                """, new { slotId, holdId, request.CourtId, request.BusinessDate, request.StartTime, request.EndTime, zone }));
            return ClaimResult(holdId, slotId, expiresAt);

            CourtSelectionResult ClaimResult(Guid claimedHold, Guid claimedSlot, DateTime expiration)
            {
                var confirmed = new CourtSelectionSlot
                {
                    SlotId = claimedSlot, HoldId = claimedHold, CourtId = request.CourtId,
                    StartTime = request.StartTime.ToString("HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture),
                    EndTime = request.EndTime.ToString("HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture),
                    StartAt = slot.StartAt, EndAt = slot.EndAt, ExpiresAt = expiration
                };
                return new(HoldId: claimedHold, SlotId: claimedSlot, ServerTime: now, ExpiresAt: expiration, Slot: confirmed);
            }
        }

        async Task<CourtSelectionResult> ReleaseAsync()
        {
            var hold = await connection.QuerySingleOrDefaultAsync<Hold>(Command("""
                SELECT id AS "Id", expires_at AS "ExpiresAt" FROM booking.court_selection_holds
                WHERE id = @HoldId AND subject_id = @subject AND session_id = @OwnerSessionId
                  AND branch_id = @BranchId AND business_date = @BusinessDate FOR UPDATE
                """, new { request.HoldId, subject, request.OwnerSessionId, request.BranchId, request.BusinessDate }));
            if (hold is null) return new("HOLD_OWNERSHIP_MISMATCH");
            var now = await connection.ExecuteScalarAsync<DateTime>(Command(DatabaseTime));
            if (request.SlotId is Guid slotId)
                await connection.ExecuteAsync(Command("""
                    DELETE FROM booking.court_selection_slots
                    WHERE id = @slotId AND hold_id = @Id
                      AND EXISTS (SELECT 1 FROM booking.court_selection_holds WHERE id = @Id AND closed_at IS NULL)
                    """, new { slotId, hold.Id }));
            await connection.ExecuteAsync(Command("""
                UPDATE booking.court_selection_holds h SET closed_at = @now
                WHERE h.id = @Id AND h.closed_at IS NULL
                  AND (@all OR NOT EXISTS (SELECT 1 FROM booking.court_selection_slots s WHERE s.hold_id = h.id))
                """, new { hold.Id, now, all = request.SlotId is null }));
            return new(HoldId: hold.Id, SlotId: request.SlotId);
        }
    }

    public async Task<IReadOnlyList<CourtSelectionChanged>> ExpireAsync(CancellationToken ct)
    {
        using var connection = factory.CreateConnection();
        var changed = await connection.QueryAsync<CourtSelectionChanged>(new CommandDefinition("""
            WITH instant AS MATERIALIZED (SELECT clock_timestamp() AS now),
            batch AS (
                SELECT id FROM booking.court_selection_holds, instant
                WHERE closed_at IS NULL AND expires_at <= instant.now
                ORDER BY expires_at LIMIT 100 FOR UPDATE OF court_selection_holds SKIP LOCKED
            ), prune AS (
                DELETE FROM booking.court_selection_operations WHERE (subject_id, idempotency_key) IN (
                    SELECT subject_id, idempotency_key FROM booking.court_selection_operations, instant
                    WHERE retain_until <= instant.now ORDER BY retain_until LIMIT 500
                    FOR UPDATE OF court_selection_operations SKIP LOCKED)
            )
            UPDATE booking.court_selection_holds h SET closed_at = instant.now
            FROM batch, instant WHERE h.id = batch.id
            RETURNING h.branch_id AS "BranchId", h.business_date AS "BusinessDate";
            """, cancellationToken: ct));
        return changed.AsList();
    }

    private static Task LockAsync(IDbConnection connection, IDbTransaction transaction, string key, CancellationToken ct) =>
        connection.ExecuteAsync(new CommandDefinition("SELECT pg_advisory_xact_lock(hashtextextended(@key, 0))",
            new { key }, transaction, cancellationToken: ct));

    private sealed class StoredOperation
    {
        public string RequestHash { get; init; } = "";
        public string Response { get; init; } = "";
    }

    private sealed class BoardMetadata
    {
        public DateTime ServerTime { get; init; }
        public string? TimeZone { get; init; }
    }

    private sealed class Hold
    {
        public Guid Id { get; init; }
        public Guid BranchId { get; init; }
        public Guid SessionId { get; init; }
        public DateOnly BusinessDate { get; init; }
        public DateTime ExpiresAt { get; init; }
    }

    private sealed class SlotCheck
    {
        public DateTime StartAt { get; init; }
        public DateTime EndAt { get; init; }
        public bool Past { get; init; }
        public bool Open { get; init; }
        public bool Booked { get; init; }
    }

    private sealed class Overlap
    {
        public Guid SlotId { get; init; }
        public Guid HoldId { get; init; }
        public bool Exact { get; init; }
    }
}
