using CourtBookingManagement.Application.CourtStatus.DTOs;
using CourtBookingManagement.Application.Branches.DTOs;

namespace CourtBookingManagement.Application.Branches.DTOs.Admin;

public sealed record BranchAdminResponse(
    Guid Id,
    string Name,
    string Description,
    string Address,
    string City,
    string District,
    decimal? Latitude,
    decimal? Longitude,
    string PhoneNumber,
    string TimeZone,
    bool SupportsInstantBooking,
    bool IsActive,
    IReadOnlyList<BranchAmenityResponse> AmenityIds,
    IReadOnlyList<BranchImageResponse> Images,
    IReadOnlyList<BranchOperatingHourResponse> OperatingHours,
    IReadOnlyList<BranchCourtResponse> Courts,
    IReadOnlyList<BranchPricingResponse> BranchPricings);

public sealed record BranchAmenityResponse(Guid Id, string Code, string Name, string? Icon);

public sealed record BranchImageResponse(string ImageUrl, short SortOrder);

public sealed record BranchOperatingHourResponse(TimeOnly OpenTime, TimeOnly CloseTime, bool IsClosed);

public sealed record BranchCourtResponse(int CourtNumber, string Name, bool IsActive);

public sealed record BranchPricingResponse(
    string PricingType,
    TimeOnly StartTime,
    TimeOnly EndTime,
    decimal PricePerHour);

public sealed record BranchAdminSearchResult(
    IReadOnlyList<BranchAdminResponse> Items,
    PaginationMetadata Metadata);