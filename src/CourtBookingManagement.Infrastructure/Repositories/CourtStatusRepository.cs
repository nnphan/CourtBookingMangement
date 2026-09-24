using CourtBookingManagement.Application.Abstractions.Data;
using CourtBookingManagement.Application.CourtStatus.DTOs;
using CourtBookingManagement.Application.CourtStatus.Interfaces;
using Dapper;
using CourtBookingManagement.Infrastructure.Persistence;
using CourtBookingManagement.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace CourtBookingManagement.Infrastructure.Repositories;

public sealed class CourtStatusRepository(ApplicationDbContext db, ISqlConnectionFactory sqlConnectionFactory) : ICourtStatusRepository
{
    public async Task<IReadOnlyList<BranchDto>> GetBranchesAsync(CancellationToken ct) =>
        await db.Branches.AsNoTracking().Where(x => x.IsActive && x.DeletedAt == null)
            .OrderBy(x => x.Name).Select(x => new BranchDto(x.Id, $"BR-{x.Id.ToString().Substring(0, 8).ToUpper()}", x.Name)).ToListAsync(ct);

    public async Task<CourtStatusResponse?> GetBoardAsync(Guid branchId, DateOnly date, CancellationToken ct)
    {
        const string sql = "SELECT * FROM booking.fn_get_court_status(@BranchId, CAST(@Date AS DATE));";
        using var connection = sqlConnectionFactory.CreateConnection();
        var d = date.ToString("yyyy-MM-dd");
        var command = new CommandDefinition(sql, new { BranchId = branchId, Date = date.ToString("yyyy-MM-dd") }, cancellationToken: ct);
        var rows = (await connection.QueryAsync<CourtStatusRow>(command)).ToList();

        var rows1 = (await connection.QueryAsync<dynamic>(command)).ToList();

        if (rows.Count == 0) return null;

        var first = rows[0];
        var courts = rows
            .Where(x => x.CourtId.HasValue)
            .GroupBy(x => x.CourtId!.Value)
            .Select(group =>
            {
                var row = group.First();
                return new CourtDto(row.CourtId!.Value, row.CourtNumber!, row.CourtName ?? row.CourtNumber!, row.CourtStatus!);
            })
            .ToList();

        var scheduleItems = rows
            .Where(x => x.ItemId.HasValue && x.ItemCourtId.HasValue)
            .Select(x => new ScheduleItemDto(
                x.ItemId!.Value,
                Enum.Parse<ScheduleItemType>(x.ItemType!, ignoreCase: true),
                x.ItemCourtId!.Value,
                x.ItemBookingtId!.Value,
                x.ItemStartTime!.Value,
                x.ItemEndTime!.Value,
                x.BookingType,
                x.BookingStatus,
                x.PaymentStatus,
                x.CustomerName,
                x.PhoneNumber,
                x.ItemTitle!,
                x.ItemColor!))
            .OrderBy(x => x.StartTime)
            .ToList();

        return new(first.BranchId, first.BranchName!, first.BoardDate, first.OpenTime, first.CloseTime, courts, scheduleItems);
    }

    public async Task<BookingDetailResponse?> GetBookingAsync(Guid id, CancellationToken ct) =>
        await db.BookingDetails.AsNoTracking().Where(x => x.BookingId == id && x.Booking.IsActive)
            .Select(x => new BookingDetailResponse(x.BookingId, x.Booking.BookingCode, x.Court.Name ?? x.Court.CourtNumber, x.Booking.Customer.FullName, x.Booking.Customer.PhoneNumber, x.BookingDate, x.StartTime, x.EndTime, x.Booking.BookingType, x.Booking.Status, "UNPAID", x.Booking.TotalAmount, 0, x.Booking.TotalAmount)).SingleOrDefaultAsync(ct);

    public Task<Guid?> GetBookingBranchIdAsync(Guid id, CancellationToken ct) =>
        db.Bookings.AsNoTracking().Where(x => x.Id == id && x.IsActive).Select(x => (Guid?)x.BranchId).SingleOrDefaultAsync(ct);

    public Task<bool> BranchExistsAsync(Guid id, CancellationToken ct) => db.Branches.AnyAsync(x => x.Id == id && x.IsActive && x.DeletedAt == null, ct);
    public Task<bool> CourtExistsAsync(Guid branchId, Guid courtId, CancellationToken ct) => db.Courts.AnyAsync(x => x.Id == courtId && x.BranchId == branchId && x.IsActive && x.DeletedAt == null, ct);
    public Task<bool> CustomerExistsAsync(Guid id, CancellationToken ct) => db.Customers.AnyAsync(x => x.Id == id && x.IsActive && x.DeletedAt == null, ct);
    public Task<bool> HasOverlappingBookingAsync(Guid courtId, DateOnly date, TimeOnly start, TimeOnly end, Guid? exclude, CancellationToken ct) => db.BookingDetails.AnyAsync(x => x.CourtId == courtId && x.BookingDate == date && x.Booking.IsActive && x.Booking.Status != "cancelled" && (exclude == null || x.BookingId != exclude) && x.StartTime < end && start < x.EndTime, ct);
    public Task<bool> HasOverlappingBlockAsync(Guid courtId, DateOnly date, TimeOnly start, TimeOnly end, CancellationToken ct) => db.CourtBlocks.AnyAsync(x => x.CourtId == courtId && x.BlockDate == date && x.IsActive && x.StartTime < end && start < x.EndTime, ct);
    public async Task<bool> IsWithinOperatingHoursAsync(Guid branchId, DateOnly date, TimeOnly start, TimeOnly end, CancellationToken ct) { var h = await db.OperatingHours.AsNoTracking().SingleOrDefaultAsync(x => x.BranchId == branchId, ct); return h != null && !h.IsClosed && start >= h.OpenTime && end <= h.CloseTime; }

    public async Task<Guid> CreateBookingAsync(Guid branchId, Guid courtId, Guid customerId, DateOnly date, TimeOnly start, TimeOnly end, string type, string? note, CancellationToken ct)
    { var id = Guid.NewGuid(); db.Bookings.Add(new Booking { Id = id, BookingCode = $"BK{DateTime.UtcNow:yyyyMMddHHmmss}", BranchId = branchId, CustomerId = customerId, Status = "pending", BookingType = type.ToUpperInvariant(), Notes = note, IsActive = true, TotalAmount = 0, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }); db.BookingDetails.Add(new BookingDetail { Id = Guid.NewGuid(), BookingId = id, CourtId = courtId, BookingDate = date, StartTime = start, EndTime = end, Status = "reserved", PriceCharged = 0, SlotRange = new NpgsqlTypes.NpgsqlRange<DateTime>(date.ToDateTime(start), true, date.ToDateTime(end), false) }); await db.SaveChangesAsync(ct); return id; }
    public async Task UpdateBookingAsync(Guid id, Guid courtId, DateOnly date, TimeOnly start, TimeOnly end, string type, string? note, CancellationToken ct) { var booking = await db.Bookings.SingleAsync(x => x.Id == id && x.IsActive, ct); var detail = await db.BookingDetails.SingleAsync(x => x.BookingId == id, ct); booking.BookingType = type.ToUpperInvariant(); booking.Notes = note; detail.CourtId = courtId; detail.BookingDate = date; detail.StartTime = start; detail.EndTime = end; detail.SlotRange = new NpgsqlTypes.NpgsqlRange<DateTime>(date.ToDateTime(start), true, date.ToDateTime(end), false); await db.SaveChangesAsync(ct); }
    public async Task CancelBookingAsync(Guid id, CancellationToken ct) { var booking = await db.Bookings.SingleAsync(x => x.Id == id && x.IsActive, ct); booking.IsActive = false; booking.Status = "cancelled"; booking.DeletedAt = DateTime.UtcNow; await db.SaveChangesAsync(ct); }

    private static string BookingColor(string type) => type.ToUpperInvariant() switch { "FIXED" => "#35A8DB", "DAILY" => "#31D37D", "FLEXIBLE" => "#E2B93B", _ => "#31D37D" };

    private sealed class CourtStatusRow
    {
        public Guid BranchId { get; init; }
        public string? BranchName { get; init; }
        public DateOnly BoardDate { get; init; }
        public TimeSpan OpenTime { get; init; }
        public TimeSpan CloseTime { get; init; }
        public Guid? CourtId { get; init; }
        public string? CourtNumber { get; init; }
        public string? CourtName { get; init; }
        public string? CourtStatus { get; init; }
        public Guid? ItemId { get; init; }
        public string? ItemType { get; init; }
        public Guid? ItemCourtId { get; init; }
        public Guid? ItemBookingtId { get; init; }
        public TimeSpan? ItemStartTime { get; init; }
        public TimeSpan? ItemEndTime { get; init; }
        public string? BookingType { get; init; }
        public string? BookingStatus { get; init; }
        public string? PaymentStatus { get; init; }
        public string? CustomerName { get; init; }
        public string? PhoneNumber { get; init; }
        public string? ItemTitle { get; init; }
        public string? ItemColor { get; init; }
    }
}