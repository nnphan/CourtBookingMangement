using System.Data;
using CourtBookingManagement.Application.Abstractions.Data;
using CourtBookingManagement.Application.Bookings.DTOs;
using CourtBookingManagement.Infrastructure.Persistence.DapperHandlers;
using CourtBookingManagement.Infrastructure.Repositories;
using Dapper;
using Microsoft.Extensions.Options;
using Npgsql;
using Xunit;

namespace CourtBookingManagement.CourtSelection.Tests;

public sealed class CourtSelectionTests
{
    [Fact]
    public async Task RangeHoldsSerializeAcrossSessionsAndExpireOrConvertAtomically()
    {
        var configured = Environment.GetEnvironmentVariable("COURT_SELECTION_TEST_PG");
        Assert.False(string.IsNullOrWhiteSpace(configured), "Set COURT_SELECTION_TEST_PG to a disposable PostgreSQL server.");
        var database = "court_selection_test_" + Guid.NewGuid().ToString("N");
        var settings = new NpgsqlConnectionStringBuilder(configured) { Pooling = false };
        await using var admin = new NpgsqlConnection(settings.ConnectionString);
        await admin.OpenAsync();
        await admin.ExecuteAsync($"""CREATE DATABASE "{database}" """);
        settings.Database = database;
        try
        {
            await RunScenariosAsync(settings.ConnectionString);
        }
        finally
        {
            await admin.ExecuteAsync($"""DROP DATABASE "{database}" WITH (FORCE)""");
        }
    }

    private static async Task RunScenariosAsync(string connectionString)
    {
        SqlMapper.AddTypeHandler(new DateOnlyTypeHandler());
        SqlMapper.AddTypeHandler(new TimeOnlyTypeHandler());
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await connection.ExecuteAsync(Fixture);
        var branchId = Guid.NewGuid();
        var courtId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var existingBookingId = Guid.NewGuid();
        var users = Enumerable.Range(0, 50).Select(_ => Guid.NewGuid()).ToArray();
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2));
        await connection.ExecuteAsync("""
            INSERT INTO core.branches VALUES (@branchId, 'Asia/Ho_Chi_Minh', true, NULL);
            INSERT INTO core.courts VALUES (@courtId, @branchId, 'active', true, NULL, 'Court 1', 1);
            INSERT INTO core.operating_hours VALUES (@branchId, false, '06:00', '23:00');
            INSERT INTO auth.users SELECT unnest(@users);
            INSERT INTO customer.customers VALUES (@customerId, 'Test Customer', '000');
            INSERT INTO booking.bookings(id, customer_id, branch_id, status, is_active)
                VALUES (@existingBookingId, @customerId, @branchId, 'confirmed', true);
            INSERT INTO booking.booking_details(id, booking_id, court_id, booking_date, start_time, end_time, slot_range, status)
                VALUES (gen_random_uuid(), @existingBookingId, @courtId, @date, '06:00', '07:00',
                    tstzrange((@date + time '06:00') AT TIME ZONE 'Asia/Ho_Chi_Minh',
                              (@date + time '07:00') AT TIME ZONE 'Asia/Ho_Chi_Minh', '[)'), 'confirmed');
            """, new { branchId, courtId, customerId, users, existingBookingId, date });
        await connection.ExecuteAsync(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "migration.sql")));
        var factory = new Factory(connectionString);
        var repository = new CourtSelectionRepository(factory, Options.Create(new CourtSelectionOptions { MaximumSlotsPerHold = 2 }));
        var booking = new BookingRepository(factory);
        CourtSelectionRequest Slot(int hour) => new(branchId, date, Guid.NewGuid(), courtId, new(hour, 0), new(hour + 1, 0), null, null);
        Task<CourtSelectionResult> Mutate(Guid user, CourtSelectionRequest request, bool release = false, Guid? key = null) =>
            repository.MutateAsync(new(user, user), key ?? Guid.NewGuid(), request, release, CancellationToken.None);
        Assert.Equal("SLOT_ALREADY_BOOKED", (await Mutate(users[0], Slot(6))).Code);

        var pair = await RaceAsync(2, 7);
        Assert.Single(pair, entry => entry.Result.Code is null);
        Assert.Single(pair, entry => entry.Result.Code == "SLOT_NOT_AVAILABLE");
        var pairWinner = pair.Single(entry => entry.Result.Code is null);
        await Mutate(pairWinner.User, pairWinner.Request with { HoldId = pairWinner.Result.HoldId }, true);

        var race = await RaceAsync(50, 8);
        var winner = Assert.Single(race, entry => entry.Result.Code is null);
        Assert.Equal(49, race.Count(entry => entry.Result.Code == "SLOT_NOT_AVAILABLE"));
        var holdId = winner.Result.HoldId!.Value;
        var owned = await repository.ReadAsync(branchId, date, winner.User, winner.Request.OwnerSessionId, default);
        Assert.Equal(holdId, Assert.Single(owned.Slots).HoldId);
        var remote = await repository.ReadAsync(branchId, date, winner.User, Guid.NewGuid(), default);
        Assert.Null(Assert.Single(remote.Slots).HoldId);
        Assert.DoesNotContain("HoldId", System.Text.Json.JsonSerializer.Serialize(remote.Slots[0]));

        var next = Slot(9) with { OwnerSessionId = winner.Request.OwnerSessionId, HoldId = holdId };
        var key = Guid.NewGuid();
        var second = await Mutate(winner.User, next, key: key);
        Assert.Equal(holdId, second.HoldId);
        Assert.Equal(second, await Mutate(winner.User, next, key: key));
        Assert.Equal("IDEMPOTENCY_KEY_REUSED", (await Mutate(winner.User, next with { EndTime = new(10, 30) }, key: key)).Code);
        Assert.Equal("HOLD_SLOT_LIMIT_EXCEEDED", (await Mutate(winner.User, Slot(10) with { OwnerSessionId = next.OwnerSessionId })).Code);
        Assert.Equal("HOLD_OWNERSHIP_MISMATCH", (await Mutate(winner.User, next with { OwnerSessionId = Guid.NewGuid() }, true)).Code);
        var afterAdd = await repository.ReadAsync(branchId, date, winner.User, next.OwnerSessionId, default);
        Assert.Equal(2, afterAdd.Slots.Count);
        Assert.All(afterAdd.Slots, slot => Assert.Equal(owned.Slots[0].ExpiresAt, slot.ExpiresAt));
        Assert.NotNull(second.Slot);
        Assert.Equal(DateTimeKind.Utc, second.Slot.StartAt.Kind);

        var existingBooking = new CreateBookingRequest
        {
            BranchId = branchId, BookingType = "FLEXIBLE", PaymentMethod = "CASH",
            Details = [new() { CourtId = courtId, BookingDate = date, StartTime = "08:30", EndTime = "09:30" }]
        };
        Assert.Null(await booking.CreateBookingAsync(existingBooking, [100_000m],
            customerId, null, null, null, null, null, default));

        var request = new CreateBookingRequest
        {
            BranchId = branchId, BookingType = "FLEXIBLE", PaymentMethod = "CASH",
            SelectionHoldId = holdId, OwnerSessionId = next.OwnerSessionId,
            Details = [new() { CourtId = courtId, BookingDate = date, StartTime = "08:00", EndTime = "10:00" }]
        };
        Task<CreateBookingResponse?> Book(Guid user, Guid customer) => booking.CreateBookingAsync(
            request, [100_000m], customer, null, null, null, null, user, default);
        Assert.Null(await Book(users.First(user => user != winner.User), customerId));
        Assert.Null(await Book(winner.User, Guid.NewGuid()));
        Assert.Equal(2, (await repository.ReadAsync(branchId, date, winner.User, next.OwnerSessionId, default)).Slots.Count);
        var created = await Book(winner.User, customerId);
        Assert.NotNull(created);
        Assert.Equal("CONFIRMED", created.Status);
        Assert.Empty((await repository.ReadAsync(branchId, date, winner.User, next.OwnerSessionId, default)).Slots);
        Assert.Equal("SLOT_ALREADY_BOOKED", (await Mutate(users[0], Slot(8))).Code);
        Assert.Equal("SLOT_IN_PAST", (await Mutate(users[0], Slot(11) with { BusinessDate = date.AddDays(-4) })).Code);
        await connection.ExecuteAsync("UPDATE core.courts SET status = 'blocked'");
        Assert.Equal("SLOT_BLOCKED", (await Mutate(users[0], Slot(11))).Code);
        await connection.ExecuteAsync("UPDATE core.courts SET status = 'active'");

        var old = await Mutate(users[0], Slot(11));
        await ExpireAsync(old.HoldId);
        Assert.Null((await Mutate(users[1], Slot(11))).Code);
        Assert.Single(await repository.ExpireAsync(default));
        Assert.Empty(await repository.ExpireAsync(default));

        var releaseRequest = Slot(12);
        var released = await Mutate(users[2], releaseRequest);
        var releaseKey = Guid.NewGuid();
        releaseRequest = releaseRequest with { HoldId = released.HoldId, SlotId = released.SlotId };
        var releaseResult = await Mutate(users[2], releaseRequest, true, releaseKey);
        Assert.Null(releaseResult.Code);
        Assert.Equal(releaseResult, await Mutate(users[2], releaseRequest, true, releaseKey));
        Assert.Null((await Mutate(users[3], Slot(12))).Code);

        var guest = new CourtSelectionOwner(Guid.NewGuid(), null);
        var guestRequest = Slot(13);
        var guestHold = await repository.MutateAsync(guest, Guid.NewGuid(), guestRequest, false, default);
        Assert.Null(guestHold.Code);
        var guestBoard = await repository.ReadAsync(branchId, date, guest.SubjectId, guestRequest.OwnerSessionId, default);
        Assert.Equal(guestHold.HoldId, Assert.Single(guestBoard.Slots, slot => slot.CourtId == courtId && slot.StartTime == "13:00:00").HoldId);
        var guestRelease = await repository.MutateAsync(guest, Guid.NewGuid(), guestRequest with { HoldId = guestHold.HoldId }, true, default);
        Assert.Null(guestRelease.Code);

        async Task<(Guid User, CourtSelectionRequest Request, CourtSelectionResult Result)[]> RaceAsync(int count, int hour)
        {
            var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var racers = users.Take(count).Select(async user =>
            {
                var target = Slot(hour);
                await ready.Task;
                return (user, target, await Mutate(user, target));
            }).ToArray();
            ready.SetResult();
            return await Task.WhenAll(racers);
        }

        Task ExpireAsync(Guid? id) => connection.ExecuteAsync(
            "UPDATE booking.court_selection_holds SET expires_at = clock_timestamp() - interval '1 second' WHERE id = @id", new { id });
    }

    private sealed record Factory(string ConnectionString) : ISqlConnectionFactory
    {
        public IDbConnection CreateConnection() => new NpgsqlConnection(ConnectionString);
    }

    private const string Fixture = """
        CREATE EXTENSION btree_gist;
        CREATE SCHEMA auth;
        CREATE SCHEMA core;
        CREATE SCHEMA customer;
        CREATE SCHEMA booking;
        CREATE TABLE auth.users(id uuid PRIMARY KEY);
        CREATE TABLE core.branches(id uuid PRIMARY KEY, time_zone text, is_active boolean, deleted_at timestamptz);
        CREATE TABLE core.courts(id uuid PRIMARY KEY, branch_id uuid, status text, is_active boolean,
            deleted_at timestamptz, name text, court_number integer);
        CREATE TABLE core.operating_hours(branch_id uuid, is_closed boolean, open_time time, close_time time);
        CREATE TABLE customer.customers(id uuid PRIMARY KEY, full_name text, phone_number text);
        CREATE TABLE booking.bookings(id uuid PRIMARY KEY, booking_code text, customer_id uuid, branch_id uuid,
            status text, booking_type text, payment_method text, subtotal_amount numeric, paid_amount numeric,
            total_amount numeric, expires_at timestamptz, confirmed_at timestamptz, notes text, hold_token_hash bytea,
            idempotency_key uuid, idempotency_request_hash bytea, is_active boolean, created_at timestamptz,
            updated_at timestamptz, deleted_at timestamptz);
        CREATE TABLE booking.booking_details(id uuid PRIMARY KEY, booking_id uuid, court_id uuid, booking_date date,
            start_time time, end_time time, slot_range tstzrange NOT NULL, status text, price_charged numeric,
            EXCLUDE USING gist (court_id WITH =, slot_range WITH &&) WHERE (upper(status) IN ('HELD', 'CONFIRMED')));
        """;
}
