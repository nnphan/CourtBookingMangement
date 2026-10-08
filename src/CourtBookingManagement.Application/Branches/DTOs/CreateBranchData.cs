using CourtBookingManagement.Application.Branches.DTOs.Requests;

namespace CourtBookingManagement.Application.Branches.DTOs;

public sealed record CreateBranchData(
    Guid Id,
    Guid OwnerId,
    Guid CreatedByUserId,
    string Name,
    string? Description,
    string Address,
    string? City,
    string? District,
    decimal? Latitude,
    decimal? Longitude,
    string? PhoneNumber,
    string TimeZone,
    bool SupportsInstantBooking,
    IReadOnlyCollection<Guid> AmenityIds,
    IReadOnlyCollection<CreateBranchImageRequest> Images,
    IReadOnlyCollection<CreateOperatingHourRequest> OperatingHours,
    IReadOnlyCollection<CreateCourtRequest> Courts,
    IReadOnlyCollection<CreateBranchPricingRequest> BranchPricings);