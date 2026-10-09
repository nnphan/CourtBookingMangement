using CourtBookingManagement.Application.Abstractions.Data;
using CourtBookingManagement.Application.CourtStatus.DTOs;
using CourtBookingManagement.Application.CourtStatus.Interfaces;
using CourtBookingManagement.Infrastructure.Persistence;
using CourtBookingManagement.Infrastructure.Persistence.Entities;
using Dapper;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace CourtBookingManagement.Infrastructure.Repositories;

public sealed class CourtStatusRepository(ApplicationDbContext db, ISqlConnectionFactory sqlConnectionFactory) : ICourtStatusRepository
{
    public async Task<BranchSearchResult> SearchBranchesAsync(BranchSearchRequest request, CancellationToken ct)
    {
        const string joinsSql = """
            FROM core.branches b
            LEFT JOIN core.operating_hours oh ON oh.branch_id = b.id
            LEFT JOIN LATERAL (
                SELECT 
                MIN(bp.price_per_hour) AS min_price,
                MAX(bp.price_per_hour) AS max_price
                FROM core.branch_pricings bp
                WHERE bp.branch_id = b.id AND bp.is_active
                  AND (@StartTime IS NULL OR (bp.start_time < @EndTime AND @StartTime < bp.end_time))
            ) pricing ON true
            """;
        const string filterSql = """
            WHERE b.is_active AND b.deleted_at IS NULL
              AND (@Keyword IS NULL OR b.name ILIKE '%' || @Keyword || '%' OR b.address ILIKE '%' || @Keyword || '%'
                   OR COALESCE(b.city, '') ILIKE '%' || @Keyword || '%' OR COALESCE(b.district, '') ILIKE '%' || @Keyword || '%')
              AND (@City IS NULL OR b.city ILIKE @City)
              AND (@District IS NULL OR b.district ILIKE @District)
              AND (@MinPrice IS NULL OR pricing.max_price >= @MinPrice)
              AND (@MaxPrice IS NULL OR pricing.min_price <= @MaxPrice)
              AND (@Date IS NULL OR (
                   oh.branch_id IS NOT NULL AND NOT oh.is_closed
                   AND EXISTS (
                       SELECT 1 FROM core.courts c
                       WHERE c.branch_id = b.id AND c.is_active AND c.deleted_at IS NULL AND LOWER(c.status) = 'active'
                         AND ((@StartTime IS NOT NULL
                               AND @StartTime >= oh.open_time AND @EndTime <= oh.close_time
                               AND NOT EXISTS (
                                   SELECT 1 FROM booking.booking_details bd
                                   JOIN booking.bookings bk ON bk.id = bd.booking_id
                                   WHERE bd.court_id = c.id AND bd.booking_date = @Date
                                     AND bd.start_time < @EndTime AND @StartTime < bd.end_time
                                     AND bk.is_active AND bk.deleted_at IS NULL
                                     AND LOWER(bk.status) NOT IN ('cancelled', 'completed')
                               ))
                              OR (@StartTime IS NULL AND EXISTS (
                                   SELECT 1
                                   FROM (
                                       SELECT oh.open_time AS candidate_time
                                       UNION
                                       SELECT bd.end_time
                                       FROM booking.booking_details bd
                                       JOIN booking.bookings bk ON bk.id = bd.booking_id
                                       WHERE bd.court_id = c.id AND bd.booking_date = @Date
                                         AND bd.end_time > oh.open_time AND bd.end_time < oh.close_time
                                         AND bk.is_active AND bk.deleted_at IS NULL
                                         AND LOWER(bk.status) NOT IN ('cancelled', 'completed')
                                   ) candidates
                                   WHERE NOT EXISTS (
                                       SELECT 1 FROM booking.booking_details bd
                                       JOIN booking.bookings bk ON bk.id = bd.booking_id
                                       WHERE bd.court_id = c.id AND bd.booking_date = @Date
                                         AND bd.start_time <= candidates.candidate_time
                                         AND candidates.candidate_time < bd.end_time
                                         AND bk.is_active AND bk.deleted_at IS NULL
                                         AND LOWER(bk.status) NOT IN ('cancelled', 'completed')
                                   )
                              )))
                   )
              ))
            """;
        var branchSql = $"""
            SELECT
            b.id AS Id,
            b.name AS Name,
            b.address AS Address,
            b.district AS District,
            b.city AS City,
            b.latitude AS Latitude,
            b.longitude AS Longitude,
            b.phone_number AS Phone,
            (
                SELECT COUNT(*)::INT
                FROM core.courts c
                WHERE c.branch_id = b.id
                  AND c.is_active = TRUE
                  AND c.deleted_at IS NULL
                  AND LOWER(c.status) = 'active'
            ) AS CourtsCount,

            pricing.min_price AS MinPrice,
            pricing.max_price AS MaxPrice,

            oh.open_time AS OpenTime,
            oh.close_time AS CloseTime,

            COALESCE(
                NOT oh.is_closed
                AND (NOW() AT TIME ZONE b.time_zone)::TIME >= oh.open_time
                AND (NOW() AT TIME ZONE b.time_zone)::TIME < oh.close_time,
                FALSE
            ) AS IsOpenNow,

            COUNT(*) OVER() AS TotalCount
            {joinsSql}
            {filterSql}
            ORDER BY CASE WHEN @SortBy = 'price_asc' THEN pricing.min_price END ASC NULLS LAST,
                     CASE WHEN @SortBy = 'price_desc' THEN pricing.min_price END DESC NULLS LAST,
                     b.name ASC
            LIMIT @PageSize OFFSET @Offset;
            """;
        var countSql = $"SELECT COUNT(*)::bigint {joinsSql} {filterSql};";

        using var connection = sqlConnectionFactory.CreateConnection();
        var offset = ((long)request.Page - 1) * request.PageSize;
        var parameters = new DynamicParameters();

        parameters.Add("Keyword" ,request.Keyword, DbType.String);

        parameters.Add("City",request.City,DbType.String);

        parameters.Add("District",request.District,DbType.String);

        parameters.Add("Date",request.Date,DbType.Date);

        parameters.Add("StartTime",request.StartTime,DbType.Time);

        parameters.Add("EndTime",request.EndTime,DbType.Time);

        parameters.Add("MinPrice", request.MinPrice,DbType.Decimal);

        parameters.Add("MaxPrice",request.MaxPrice,DbType.Decimal);

        parameters.Add("SortBy",request.SortBy,DbType.String);

        parameters.Add("PageSize",request.PageSize,DbType.Int32);

        parameters.Add("Offset", offset, DbType.Int32);

        //var parameters = new  DynamicParameters();
        //{
        //    request.Keyword,
        //    request.City,
        //    request.District,
        //    request.Date,
        //    request.StartTime,
        //    request.EndTime,
        //    request.MinPrice,
        //    request.MaxPrice,
        //    request.SortBy,
        //    request.PageSize,
        //    Offset = offset
        //};
        var branches = (await connection.QueryAsync<BranchSearchRow>(
            new CommandDefinition(branchSql, parameters, cancellationToken: ct))).AsList();

        var totalCount = branches.Count > 0
            ? branches[0].TotalCount
            : await connection.ExecuteScalarAsync<long>(new CommandDefinition(countSql, parameters, cancellationToken: ct));
        var branchIds = branches.Select(branch => branch.Id).ToArray();
        List<BranchImageRow> images = branchIds.Length == 0
            ? []
            : (await connection.QueryAsync<BranchImageRow>(new CommandDefinition("""
                SELECT branch_id AS BranchId, image_url AS ImageUrl
                FROM core.branch_images
                WHERE branch_id = ANY(@BranchIds) AND is_active
                ORDER BY sort_order;
                """, new { BranchIds = branchIds }, cancellationToken: ct))).AsList();
        List<BranchAmenityRow> amenities = branchIds.Length == 0
            ? []
            : (await connection.QueryAsync<BranchAmenityRow>(new CommandDefinition("""
                SELECT ba.branch_id AS BranchId, a.id AS Id, a.code AS Code, a.name AS Name
                FROM core.branches_amenities ba
                JOIN core.amenities a ON a.id = ba.amenity_id
                WHERE ba.branch_id = ANY(@BranchIds)
                ORDER BY a.name;
                """, new { BranchIds = branchIds }, cancellationToken: ct))).AsList();

        var imageLookup = images.GroupBy(image => image.BranchId).ToDictionary(group => group.Key, group => (IReadOnlyList<string>)group.Select(image => image.ImageUrl).ToArray());
        var amenityLookup = amenities.GroupBy(amenity => amenity.BranchId).ToDictionary(group => group.Key, group => (IReadOnlyList<BranchAmenityDto>)group.Select(amenity => new BranchAmenityDto(amenity.Id, amenity.Code, amenity.Name)).ToArray());
        var items = branches.Select(branch => new BranchDiscoveryDto(
            branch.Id,
            branch.Name,
            branch.Address,
            branch.District,
            branch.City,
            branch.Latitude.HasValue && branch.Longitude.HasValue
                ? new BranchCoordinatesDto(branch.Latitude.Value, branch.Longitude.Value)
                : null,
            branch.Phone,
            imageLookup.GetValueOrDefault(branch.Id, []),
            amenityLookup.GetValueOrDefault(branch.Id, []),
            branch.CourtsCount,
            new BranchPriceRangeDto(branch.MinPrice, branch.MaxPrice, "VND"),
            branch.OpenTime.HasValue && branch.CloseTime.HasValue
                ? new BranchOperatingHoursDto(branch.OpenTime.Value.ToString("HH:mm"), branch.CloseTime.Value.ToString("HH:mm"), "Thứ 2 - Chủ Nhật")
                : null,
            branch.IsOpenNow)).ToArray();

        var totalPages = (int)Math.Ceiling(totalCount / (double)request.PageSize);
        return new BranchSearchResult(items, new PaginationMetadata(
            request.Page,
            request.PageSize,
            totalCount,
            totalPages,
            request.Page > 1,
            request.Page < totalPages));
    }

    private sealed class BranchSearchRow
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string Address { get; init; } = string.Empty;
        public string? District { get; init; }
        public string? City { get; init; }
        public decimal? Latitude { get; init; }
        public decimal? Longitude { get; init; }
        public string? Phone { get; init; }
        public int CourtsCount { get; init; }
        public decimal? MinPrice { get; init; }
        public decimal? MaxPrice { get; init; }
        public TimeOnly? OpenTime { get; init; }
        public TimeOnly? CloseTime { get; init; }
        public bool IsOpenNow { get; init; }
        public long TotalCount { get; init; }
    }

    private sealed class BranchImageRow
    {
        public Guid BranchId { get; init; }
        public string ImageUrl { get; init; } = string.Empty;
    }

    private sealed class BranchAmenityRow
    {
        public Guid BranchId { get; init; }
        public Guid Id { get; init; }
        public string Code { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
    }

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
            .GroupBy(x => x.CourtId)
            .Select(group =>
            {
                var row = group.First();
                return new CourtDto(row.CourtId, row.CourtNumber!, row.CourtName ?? row.CourtNumber!, row.CourtStatus!);
            })
            .ToList();

        var scheduleItems = rows
            .Where(x => x.BookingId != null)
            .Select(x => new ScheduleItemDto(
                x.BookingId!.Value,
                x.CourtId,
                x.StartTime!.Value,
                x.EndTime!.Value,
                x.BookingDate!.Value,
                x.BookingType,
                x.BookingStatus,
                x.PaymentStatus,
                x.CustomerName,
                x.PhoneNumber,
                x.Title!,
                x.Color!))
            .OrderBy(x => x.StartTime)
            .ToList();

        return new CourtStatusResponse(first.BranchId, first.BranchName!, date, first.OpenTime, first.CloseTime, courts, scheduleItems);
    }

    public async Task<BookingDetailResponse?> GetBookingAsync(Guid id, CancellationToken ct) =>
        await db.BookingDetails.AsNoTracking().Where(x => x.BookingId == id && x.Booking.IsActive)
            .Select(x => new BookingDetailResponse(x.BookingId, x.Booking.BookingCode, x.Court.Name ?? x.Court.CourtNumber.ToString(), x.Booking.Customer.FullName, x.Booking.Customer.PhoneNumber, x.BookingDate, x.StartTime, x.EndTime, x.Booking.BookingType, x.Booking.Status, "UNPAID", x.Booking.TotalAmount, 0, x.Booking.TotalAmount)).SingleOrDefaultAsync(ct);

    public Task<Guid?> GetBookingBranchIdAsync(Guid id, CancellationToken ct) =>
        db.Bookings.AsNoTracking().Where(x => x.Id == id && x.IsActive).Select(x => (Guid?)x.BranchId).SingleOrDefaultAsync(ct);

    public Task<bool> BranchExistsAsync(Guid id, CancellationToken ct) => db.Branches.AnyAsync(x => x.Id == id && x.IsActive && x.DeletedAt == null, ct);
    public Task<bool> CourtExistsAsync(Guid branchId, Guid courtId, CancellationToken ct) => db.Courts.AnyAsync(x => x.Id == courtId && x.BranchId == branchId && x.IsActive && x.DeletedAt == null, ct);
    public Task<bool> CustomerExistsAsync(Guid id, CancellationToken ct) => db.Customers.AnyAsync(x => x.Id == id && x.IsActive && x.DeletedAt == null, ct);
    public Task<bool> HasOverlappingBookingAsync(Guid courtId, DateOnly date, TimeOnly start, TimeOnly end, Guid? exclude, CancellationToken ct) => db.BookingDetails.AnyAsync(x => x.CourtId == courtId && x.BookingDate == date && x.Booking.IsActive && x.Booking.Status != "cancelled" && (exclude == null || x.BookingId != exclude) && x.StartTime < end && start < x.EndTime, ct);
    public async Task<bool> IsWithinOperatingHoursAsync(Guid branchId, DateOnly date, TimeOnly start, TimeOnly end, CancellationToken ct) { var h = await db.OperatingHours.AsNoTracking().SingleOrDefaultAsync(x => x.BranchId == branchId, ct); return h != null && !h.IsClosed && start >= h.OpenTime && end <= h.CloseTime; }

    public async Task<Guid> CreateBookingAsync(Guid branchId, Guid courtId, Guid customerId, DateOnly date, TimeOnly start, TimeOnly end, string type, string? note, CancellationToken ct)
    { var id = Guid.NewGuid(); db.Bookings.Add(new Booking { Id = id, BookingCode = $"BK{DateTime.UtcNow:yyyyMMddHHmmss}", BranchId = branchId, CustomerId = customerId, Status = "pending", BookingType = type.ToUpperInvariant(), Notes = note, IsActive = true, TotalAmount = 0, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }); db.BookingDetails.Add(new BookingDetail { Id = Guid.NewGuid(), BookingId = id, CourtId = courtId, BookingDate = date, StartTime = start, EndTime = end, Status = "reserved", PriceCharged = 0, SlotRange = new NpgsqlTypes.NpgsqlRange<DateTime>(date.ToDateTime(start), true, date.ToDateTime(end), false) }); await db.SaveChangesAsync(ct); return id; }
    public async Task UpdateBookingAsync(Guid id, Guid courtId, DateOnly date, TimeOnly start, TimeOnly end, string type, string? note, CancellationToken ct) { var booking = await db.Bookings.SingleAsync(x => x.Id == id && x.IsActive, ct); var detail = await db.BookingDetails.SingleAsync(x => x.BookingId == id, ct); booking.BookingType = type.ToUpperInvariant(); booking.Notes = note; detail.CourtId = courtId; detail.BookingDate = date; detail.StartTime = start; detail.EndTime = end; detail.SlotRange = new NpgsqlTypes.NpgsqlRange<DateTime>(date.ToDateTime(start), true, date.ToDateTime(end), false); await db.SaveChangesAsync(ct); }
    public async Task CancelBookingAsync(Guid id, CancellationToken ct) { var booking = await db.Bookings.SingleAsync(x => x.Id == id && x.IsActive, ct); booking.IsActive = false; booking.Status = "cancelled"; booking.DeletedAt = DateTime.UtcNow; await db.SaveChangesAsync(ct); }

    private static string BookingColor(string type) => type.ToUpperInvariant() switch { "FIXED" => "#35A8DB", "DAILY" => "#31D37D", "FLEXIBLE" => "#E2B93B", _ => "#31D37D" };

    public sealed class CourtStatusRow
    {
        public Guid BranchId { get; set; }

        public string BranchName { get; set; } = string.Empty;

        public TimeOnly OpenTime { get; set; }

        public TimeOnly CloseTime { get; set; }

        public Guid CourtId { get; set; }

        public string CourtNumber { get; set; } = string.Empty;

        public string CourtName { get; set; } = string.Empty;

        public string CourtStatus { get; set; } = string.Empty;

        public Guid? BookingId { get; set; }

        public TimeOnly? StartTime { get; set; }

        public TimeOnly? EndTime { get; set; }

        public DateOnly? BookingDate { get; set; }

        public string? BookingType { get; set; }

        public string? BookingStatus { get; set; }

        public string? PaymentStatus { get; set; }

        public string? CustomerName { get; set; }

        public string? PhoneNumber { get; set; }

        public string? Title { get; set; }

        public string? Color { get; set; }
    }
}