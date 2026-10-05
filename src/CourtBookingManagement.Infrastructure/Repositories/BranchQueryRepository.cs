using CourtBookingManagement.Application.Abstractions.Data;
using CourtBookingManagement.Application.Branches.DTOs;
using CourtBookingManagement.Application.Branches.Interfaces;
using Dapper;

namespace CourtBookingManagement.Infrastructure.Repositories;

public sealed class BranchQueryRepository(ISqlConnectionFactory sqlConnectionFactory)
    : IBranchQueryRepository
{
    public async Task<BranchDetailsResponse?> GetBranchDetailsAsync(
        Guid branchId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                id AS Id,
                owner_id AS OwnerId,
                name AS Name,
                address AS Address,
                city AS City,
                district AS District,
                latitude AS Latitude,
                longitude AS Longitude,
                phone_number AS PhoneNumber,
                is_active AS IsActive,
                time_zone AS TimeZone,
                supports_instant_booking AS SupportsInstantBooking
            FROM core.branches
            WHERE id = @Id
              AND deleted_at IS NULL;

            SELECT id AS Id, image_url AS ImageUrl, sort_order AS SortOrder
            FROM core.branch_images
            WHERE branch_id = @Id
              AND is_active = TRUE
            ORDER BY sort_order;

            SELECT a.id AS Id, a.code AS Code, a.name AS Name, a.icon AS Icon
            FROM core.branches_amenities ba
            JOIN core.amenities a ON a.id = ba.amenity_id
            WHERE ba.branch_id = @Id
            ORDER BY a.name;

            SELECT id AS Id, court_number AS CourtNumber, name AS Name, status AS Status
            FROM core.courts
            WHERE branch_id = @Id
              AND deleted_at IS NULL
            ORDER BY court_number;

            SELECT id AS Id, open_time AS OpenTime, close_time AS CloseTime, is_closed AS IsClosed
            FROM core.operating_hours
            WHERE branch_id = @Id;

            SELECT id AS Id, pricing_type AS PricingType, start_time AS StartTime,
                   end_time AS EndTime, price_per_hour AS PricePerHour
            FROM core.branch_pricings
            WHERE branch_id = @Id
              AND is_active = TRUE
            ORDER BY start_time;
            """;

        using var connection = sqlConnectionFactory.CreateConnection();
        using var results = await connection.QueryMultipleAsync(
            new CommandDefinition(sql, new { Id = branchId }, cancellationToken: cancellationToken));

        var branch = await results.ReadFirstOrDefaultAsync<BranchRow>();
        var images = (await results.ReadAsync<BranchImageResponse>()).AsList();
        var amenities = (await results.ReadAsync<AmenityResponse>()).AsList();
        var courts = (await results.ReadAsync<CourtResponse>()).AsList();
        var operatingHourRows = (await results.ReadAsync<OperatingHourRow>()).AsList();
        var pricingRows = (await results.ReadAsync<BranchPricingRow>()).AsList();

        if (branch is null)
        {
            return null;
        }

        var pricings = pricingRows.Select(pricing => new BranchPricingResponse
        {
            Id = pricing.Id,
            PricingType = pricing.PricingType,
            StartTime = pricing.StartTime.ToTimeSpan(),
            EndTime = pricing.EndTime.ToTimeSpan(),
            PricePerHour = pricing.PricePerHour
        }).ToArray();

        return new BranchDetailsResponse
        {
            Id = branch.Id,
            OwnerId = branch.OwnerId,
            Name = branch.Name,
            Address = branch.Address,
            City = branch.City,
            District = branch.District,
            Latitude = branch.Latitude,
            Longitude = branch.Longitude,
            PhoneNumber = branch.PhoneNumber,
            IsActive = branch.IsActive,
            TimeZone = branch.TimeZone,
            SupportsInstantBooking = branch.SupportsInstantBooking,
            Images = images,
            Amenities = amenities,
            Courts = courts,
            OperatingHours = operatingHourRows.Select(hour => new OperatingHourResponse
            {
                Id = hour.Id,
                OpenTime = hour.OpenTime.ToTimeSpan(),
                CloseTime = hour.CloseTime.ToTimeSpan(),
                IsClosed = hour.IsClosed
            }).ToArray(),
            Pricings = pricings,
            Statistics = new BranchStatisticsResponse
            {
                TotalCourts = courts.Count,
                TotalAmenities = amenities.Count,
                TotalImages = images.Count,
                MinPrice = pricings.Length == 0 ? null : pricings.Min(pricing => pricing.PricePerHour),
                MaxPrice = pricings.Length == 0 ? null : pricings.Max(pricing => pricing.PricePerHour)
            }
        };
    }

    private sealed class BranchRow
    {
        public Guid Id { get; init; }
        public Guid OwnerId { get; init; }
        public string Name { get; init; } = string.Empty;
        public string Address { get; init; } = string.Empty;
        public string? City { get; init; }
        public string? District { get; init; }
        public decimal? Latitude { get; init; }
        public decimal? Longitude { get; init; }
        public string? PhoneNumber { get; init; }
        public bool IsActive { get; init; }
        public string TimeZone { get; init; } = string.Empty;
        public bool SupportsInstantBooking { get; init; }
    }

    private sealed class OperatingHourRow
    {
        public Guid Id { get; init; }
        public TimeOnly OpenTime { get; init; }
        public TimeOnly CloseTime { get; init; }
        public bool IsClosed { get; init; }
    }

    private sealed class BranchPricingRow
    {
        public Guid Id { get; init; }
        public string PricingType { get; init; } = string.Empty;
        public TimeOnly StartTime { get; init; }
        public TimeOnly EndTime { get; init; }
        public decimal PricePerHour { get; init; }
    }
}