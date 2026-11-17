using System.Data;
using CourtBookingManagement.Application.Abstractions.Data;
using CourtBookingManagement.Application.Bookings.Interfaces;
using CourtBookingManagement.Application.Bookings.Models;
using CourtBookingManagement.Domain.Bookings;
using Dapper;

namespace CourtBookingManagement.Infrastructure.Repositories;

public sealed class BookingExpirationRepository(ISqlConnectionFactory connectionFactory)
    : IBookingExpirationRepository
{
    public async Task<IReadOnlyList<ExpiredBookingHold>> ExpireBatchAsync(
        int batchSize,
        CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);

        var rows = await connection.QueryAsync<ExpiredBookingSlot>(new CommandDefinition("""
            WITH expired AS MATERIALIZED
            (
                SELECT id
                FROM booking.bookings
                WHERE LOWER(status) = 'pending_payment'
                  AND expires_at <= NOW()
                  AND is_active = TRUE
                  AND deleted_at IS NULL
                ORDER BY expires_at, id
                LIMIT @BatchSize
                FOR UPDATE SKIP LOCKED
            )
            SELECT
                expired.id AS BookingId,
                detail.court_id AS CourtId,
                detail.booking_date AS BookingDate,
                detail.start_time AS StartTime,
                detail.end_time AS EndTime
            FROM expired
            JOIN booking.booking_details detail ON detail.booking_id = expired.id;
            """, new { BatchSize = batchSize }, transaction, cancellationToken: cancellationToken));

        var slots = rows.AsList();
        if (slots.Count == 0)
        {
            transaction.Commit();
            return [];
        }

        var bookingIds = slots.Select(slot => slot.BookingId).Distinct().ToArray();
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE booking.bookings
            SET status = 'expired',
                updated_at = NOW()
            WHERE id = ANY(@BookingIds)
              AND LOWER(status) = 'pending_payment'
              AND expires_at <= NOW();

            UPDATE booking.booking_details
            SET status = 'expired'
            WHERE booking_id = ANY(@BookingIds)
              AND LOWER(status) = 'held';

            UPDATE payment.payment_transactions
            SET status = 'EXPIRED',
                expired_at = COALESCE(expired_at, NOW()),
                updated_at = NOW()
            WHERE booking_id = ANY(@BookingIds)
              AND status IN ('PENDING', 'PROCESSING');
            """, new { BookingIds = bookingIds }, transaction, cancellationToken: cancellationToken));
        transaction.Commit();

        return slots
            .GroupBy(slot => slot.BookingId)
            .Select(group => new ExpiredBookingHold(
                group.Key,
                group.Select(slot => new BookingSlotResource(
                    slot.CourtId,
                    slot.BookingDate,
                    slot.StartTime,
                    slot.EndTime)).ToArray()))
            .ToArray();
    }

    private sealed class ExpiredBookingSlot
    {
        public Guid BookingId { get; init; }
        public Guid CourtId { get; init; }
        public DateOnly BookingDate { get; init; }
        public TimeOnly StartTime { get; init; }
        public TimeOnly EndTime { get; init; }
    }
}
