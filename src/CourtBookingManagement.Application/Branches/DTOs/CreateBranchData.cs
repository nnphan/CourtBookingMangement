using CourtBookingManagement.Application.Branches.DTOs.Requests;

namespace CourtBookingManagement.Application.Branches.DTOs;

public sealed record CreateBranchData(
    Guid Id,
    Guid OwnerId,
    Guid CreatedByUserId,
    string Name,
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
    IReadOnlyCollection<CreateOperatingHourRequest> OperatingHours);