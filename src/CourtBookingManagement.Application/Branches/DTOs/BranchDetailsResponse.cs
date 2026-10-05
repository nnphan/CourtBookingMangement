namespace CourtBookingManagement.Application.Branches.DTOs;

public sealed class BranchDetailsResponse
{
    public Guid Id { get; set; }
    public Guid OwnerId { get; set; }
    public string Name { get; set; } = default!;
    public string Address { get; set; } = default!;
    public string? City { get; set; }
    public string? District { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string? PhoneNumber { get; set; }
    public bool IsActive { get; set; }
    public string TimeZone { get; set; } = default!;
    public bool SupportsInstantBooking { get; set; }
    public IReadOnlyList<BranchImageResponse> Images { get; set; } = [];
    public IReadOnlyList<AmenityResponse> Amenities { get; set; } = [];
    public IReadOnlyList<CourtResponse> Courts { get; set; } = [];
    public IReadOnlyList<OperatingHourResponse> OperatingHours { get; set; } = [];
    public IReadOnlyList<BranchPricingResponse> Pricings { get; set; } = [];
    public BranchStatisticsResponse Statistics { get; set; } = new();
}

public sealed class BranchImageResponse
{
    public Guid Id { get; set; }
    public string ImageUrl { get; set; } = default!;
    public short SortOrder { get; set; }
}

public sealed class AmenityResponse
{
    public Guid Id { get; set; }
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? Icon { get; set; }
}

public sealed class CourtResponse
{
    public Guid Id { get; set; }
    public int CourtNumber { get; set; }
    public string? Name { get; set; }
    public string Status { get; set; } = default!;
}

public sealed class OperatingHourResponse
{
    public Guid Id { get; set; }
    public TimeSpan OpenTime { get; set; }
    public TimeSpan CloseTime { get; set; }
    public bool IsClosed { get; set; }
}

public sealed class BranchPricingResponse
{
    public Guid Id { get; set; }
    public string PricingType { get; set; } = default!;
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public decimal PricePerHour { get; set; }
}

public sealed class BranchStatisticsResponse
{
    public int TotalCourts { get; set; }
    public int TotalAmenities { get; set; }
    public int TotalImages { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
}