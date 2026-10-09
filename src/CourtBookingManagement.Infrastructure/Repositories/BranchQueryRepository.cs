using CourtBookingManagement.Application.Abstractions.Data;
using CourtBookingManagement.Application.Amenities.Models.Responses;
using CourtBookingManagement.Application.Branches.DTOs;
using CourtBookingManagement.Application.Branches.DTOs.Requests;
using CourtBookingManagement.Application.Branches.Interfaces;
using CourtBookingManagement.Application.CourtStatus.DTOs;
using Dapper;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using AdminBranchAmenityResponse = CourtBookingManagement.Application.Branches.DTOs.Admin.BranchAmenityResponse;
using AdminBranchCourtResponse = CourtBookingManagement.Application.Branches.DTOs.Admin.BranchCourtResponse;
using AdminBranchImageResponse = CourtBookingManagement.Application.Branches.DTOs.Admin.BranchImageResponse;
using AdminBranchOperatingHourResponse = CourtBookingManagement.Application.Branches.DTOs.Admin.BranchOperatingHourResponse;
using AdminBranchPricingResponse = CourtBookingManagement.Application.Branches.DTOs.Admin.BranchPricingResponse;
using AdminBranchResponse = CourtBookingManagement.Application.Branches.DTOs.Admin.BranchAdminResponse;
using BranchAdminSearchResult = CourtBookingManagement.Application.Branches.DTOs.Admin.BranchAdminSearchResult;

namespace CourtBookingManagement.Infrastructure.Repositories;

public sealed class BranchQueryRepository(
    ISqlConnectionFactory sqlConnectionFactory,
    ILogger<BranchQueryRepository> logger)
    : IBranchQueryRepository
{
    public async Task<BranchAdminSearchResult> SearchBranchesAsync(
        BranchAdminSearchRequest request,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        logger.LogInformation(
            "Searching admin branches. Keyword: {Keyword}, City: {City}, District: {District}, IsActive: {IsActive}, PageNumber: {PageNumber}, PageSize: {PageSize}",
            request.Keyword,
            request.City,
            request.District,
            request.IsActive,
            request.PageNumber,
            request.PageSize);

        try
        {
            var filters = new List<string> { "b.deleted_at IS NULL" };
            var parameters = new DynamicParameters();
            if (!string.IsNullOrWhiteSpace(request.Keyword))
            {
                filters.Add("(b.name ILIKE '%' || @Keyword || '%' OR b.address ILIKE '%' || @Keyword || '%' OR COALESCE(b.city, '') ILIKE '%' || @Keyword || '%' OR COALESCE(b.district, '') ILIKE '%' || @Keyword || '%')");
                parameters.Add("Keyword", request.Keyword);
            }

            if (!string.IsNullOrWhiteSpace(request.City))
            {
                filters.Add("b.city ILIKE @City");
                parameters.Add("City", request.City);
            }

            if (!string.IsNullOrWhiteSpace(request.District))
            {
                filters.Add("b.district ILIKE @District");
                parameters.Add("District", request.District);
            }

            if (request.IsActive.HasValue)
            {
                filters.Add("b.is_active = @IsActive");
                parameters.Add("IsActive", request.IsActive.Value);
            }

            var whereSql = string.Join(" AND ", filters);
            var offset = ((long)request.PageNumber - 1) * request.PageSize;
            parameters.Add("PageSize", request.PageSize);
            parameters.Add("Offset", offset);

            var pageSql = $"""
                SELECT b.id AS Id,
                       b.name AS Name,
                       COALESCE(b.description, '') AS Description,
                       b.address AS Address,
                       COALESCE(b.city, '') AS City,
                       COALESCE(b.district, '') AS District,
                       b.latitude AS Latitude,
                       b.longitude AS Longitude,
                       COALESCE(b.phone_number, '') AS PhoneNumber,
                       b.time_zone AS TimeZone,
                       b.supports_instant_booking AS SupportsInstantBooking,
                       b.is_active AS IsActive
                FROM core.branches b
                WHERE {whereSql}
                ORDER BY b.name ASC, b.id ASC
                LIMIT @PageSize OFFSET @Offset;

                SELECT COUNT(*)::bigint
                FROM core.branches b
                WHERE {whereSql};
                """;

            using var connection = sqlConnectionFactory.CreateConnection();
            using var pageResults = await connection.QueryMultipleAsync(new CommandDefinition(
                pageSql,
                parameters,
                cancellationToken: cancellationToken));

            var branches = (await pageResults.ReadAsync<BranchAdminRow>()).AsList();
            var totalCount = await pageResults.ReadSingleAsync<long>();
            var branchIds = branches.Select(branch => branch.Id).ToArray();

            const string childSql = """
                SELECT ba.branch_id AS BranchId, a.id AS Id, a.code AS Code, a.name AS Name, a.icon AS Icon
                FROM core.branches_amenities ba
                JOIN core.amenities a ON a.id = ba.amenity_id
                WHERE ba.branch_id = ANY(@BranchIds)
                ORDER BY ba.branch_id, a.name;

                SELECT branch_id AS BranchId, image_url AS ImageUrl, sort_order AS SortOrder
                FROM core.branch_images
                WHERE branch_id = ANY(@BranchIds)
                ORDER BY branch_id, sort_order;

                SELECT branch_id AS BranchId, open_time AS OpenTime, close_time AS CloseTime, is_closed AS IsClosed
                FROM core.operating_hours
                WHERE branch_id = ANY(@BranchIds)
                ORDER BY branch_id, open_time;

                SELECT branch_id AS BranchId, court_number AS CourtNumber, COALESCE(name, '') AS Name, is_active AS IsActive
                FROM core.courts
                WHERE branch_id = ANY(@BranchIds)
                  AND deleted_at IS NULL
                ORDER BY branch_id, court_number;

                SELECT branch_id AS BranchId, pricing_type AS PricingType, start_time AS StartTime,
                       end_time AS EndTime, price_per_hour AS PricePerHour
                FROM core.branch_pricings
                WHERE branch_id = ANY(@BranchIds)
                  AND is_active = TRUE
                ORDER BY branch_id, start_time;
                """;

            using var childResults = await connection.QueryMultipleAsync(new CommandDefinition(
                childSql,
                new { BranchIds = branchIds },
                cancellationToken: cancellationToken));

            var amenities = (await childResults.ReadAsync<BranchAmenityRow>()).AsList();
            var images = (await childResults.ReadAsync<BranchImageRow>()).AsList();
            var operatingHours = (await childResults.ReadAsync<BranchOperatingHourRow>()).AsList();
            var courts = (await childResults.ReadAsync<BranchCourtRow>()).AsList();
            var pricings = (await childResults.ReadAsync<BranchAdminPricingRow>()).AsList();

            var amenityLookup = amenities.GroupBy(item => item.BranchId)
                .ToDictionary(group => group.Key, group => (IReadOnlyList<AdminBranchAmenityResponse>)group
                    .Select(item => new AdminBranchAmenityResponse(item.Id, item.Code, item.Name, item.Icon)).ToArray());
            var imageLookup = images.GroupBy(item => item.BranchId)
                .ToDictionary(group => group.Key, group => (IReadOnlyList<AdminBranchImageResponse>)group
                    .Select(item => new AdminBranchImageResponse(item.ImageUrl, item.SortOrder)).ToArray());
            var operatingHourLookup = operatingHours.GroupBy(item => item.BranchId)
                .ToDictionary(group => group.Key, group => (IReadOnlyList<AdminBranchOperatingHourResponse>)group
                    .Select(item => new AdminBranchOperatingHourResponse(item.OpenTime, item.CloseTime, item.IsClosed)).ToArray());
            var courtLookup = courts.GroupBy(item => item.BranchId)
                .ToDictionary(group => group.Key, group => (IReadOnlyList<AdminBranchCourtResponse>)group
                    .Select(item => new AdminBranchCourtResponse(item.CourtNumber, item.Name, item.IsActive)).ToArray());
            var pricingLookup = pricings.GroupBy(item => item.BranchId)
                .ToDictionary(group => group.Key, group => (IReadOnlyList<AdminBranchPricingResponse>)group
                    .Select(item => new AdminBranchPricingResponse(item.PricingType, item.StartTime, item.EndTime, item.PricePerHour)).ToArray());

            var items = branches.Select(branch => new AdminBranchResponse(
                branch.Id,
                branch.Name,
                branch.Description,
                branch.Address,
                branch.City,
                branch.District,
                branch.Latitude,
                branch.Longitude,
                branch.PhoneNumber,
                branch.TimeZone,
                branch.SupportsInstantBooking,
                branch.IsActive,
                amenityLookup.GetValueOrDefault(branch.Id, Array.Empty<AdminBranchAmenityResponse>()),
                imageLookup.GetValueOrDefault(branch.Id, Array.Empty<AdminBranchImageResponse>()),
                operatingHourLookup.GetValueOrDefault(branch.Id, Array.Empty<AdminBranchOperatingHourResponse>()),
                courtLookup.GetValueOrDefault(branch.Id, Array.Empty<AdminBranchCourtResponse>()),
                pricingLookup.GetValueOrDefault(branch.Id, Array.Empty<AdminBranchPricingResponse>()))).ToArray();

            var totalPages = (int)Math.Ceiling(totalCount / (double)request.PageSize);
            stopwatch.Stop();
            logger.LogInformation(
                "Admin branch query completed in {QueryExecutionTimeMs} ms. BranchCount: {BranchCount}, TotalCount: {TotalCount}",
                stopwatch.Elapsed.TotalMilliseconds,
                items.Length,
                totalCount);

            return new BranchAdminSearchResult(items, new PaginationMetadata(
                request.PageNumber,
                request.PageSize,
                totalCount,
                totalPages,
                request.PageNumber > 1,
                request.PageNumber < totalPages));
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            logger.LogError(
                exception,
                "Admin branch query failed after {QueryExecutionTimeMs} ms. Keyword: {Keyword}, City: {City}, District: {District}, IsActive: {IsActive}, PageNumber: {PageNumber}, PageSize: {PageSize}",
                stopwatch.Elapsed.TotalMilliseconds,
                request.Keyword,
                request.City,
                request.District,
                request.IsActive,
                request.PageNumber,
                request.PageSize);
            throw;
        }
    }

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

    private sealed class BranchAdminRow
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public string Address { get; init; } = string.Empty;
        public string City { get; init; } = string.Empty;
        public string District { get; init; } = string.Empty;
        public decimal? Latitude { get; init; }
        public decimal? Longitude { get; init; }
        public string PhoneNumber { get; init; } = string.Empty;
        public string TimeZone { get; init; } = string.Empty;
        public bool SupportsInstantBooking { get; init; }
        public bool IsActive { get; init; }
    }

    private sealed class BranchAmenityRow
    {
        public Guid BranchId { get; init; }
        public Guid Id { get; init; }
        public string Code { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string? Icon { get; init; }
    }

    private sealed class BranchImageRow
    {
        public Guid BranchId { get; init; }
        public string ImageUrl { get; init; } = string.Empty;
        public short SortOrder { get; init; }
    }

    private sealed class BranchOperatingHourRow
    {
        public Guid BranchId { get; init; }
        public TimeOnly OpenTime { get; init; }
        public TimeOnly CloseTime { get; init; }
        public bool IsClosed { get; init; }
    }

    private sealed class BranchCourtRow
    {
        public Guid BranchId { get; init; }
        public int CourtNumber { get; init; }
        public string Name { get; init; } = string.Empty;
        public bool IsActive { get; init; }
    }

    private sealed class BranchAdminPricingRow
    {
        public Guid BranchId { get; init; }
        public string PricingType { get; init; } = string.Empty;
        public TimeOnly StartTime { get; init; }
        public TimeOnly EndTime { get; init; }
        public decimal PricePerHour { get; init; }
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