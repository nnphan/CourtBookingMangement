namespace CourtBookingManagement.Application.CourtStatus.DTOs;

public sealed record GetCourtStatusRequest
{
    public Guid BranchId { get; init; }

    public DateOnly Date { get; init; }
}

public enum ScheduleItemType { Booking, Block, Event }

public sealed record BranchDto(Guid Id, string Code, string Name);

public sealed record BranchSearchRequest
{
    public string? Keyword { get; init; }
    public string? City { get; init; }
    public string? District { get; init; }
    public DateOnly? Date { get; init; }
    public TimeOnly? StartTime { get; init; }
    public TimeOnly? EndTime { get; init; }
    public decimal? MinPrice { get; init; }
    public decimal? MaxPrice { get; init; }
    public string? SortBy { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 12;
}

public sealed record BranchSearchResult(IReadOnlyList<BranchDiscoveryDto> Items, PaginationMetadata Metadata);

public sealed record PaginationMetadata(
    int PageNumber,
    int PageSize,
    long TotalCount,
    int TotalPages,
    bool HasPreviousPage,
    bool HasNextPage);

public sealed record BranchDiscoveryDto(
    Guid Id,
    string Name,
    string Address,
    string? District,
    string? City,
    BranchCoordinatesDto? Coordinates,
    string? Phone,
    IReadOnlyList<string> Images,
    IReadOnlyList<BranchAmenityDto> Amenities,
    int CourtsCount,
    BranchPriceRangeDto PriceRange,
    BranchOperatingHoursDto? OperatingHours,
    bool IsOpenNow);

public sealed record BranchCoordinatesDto(decimal Lat, decimal Lng);
public sealed record BranchAmenityDto(Guid Id, string Code, string Name);
public sealed record BranchPriceRangeDto(decimal? Min, decimal? Max, string Currency);
public sealed record BranchOperatingHoursDto(string Open, string Close, string DaysDescription);

public sealed record CourtDto(Guid CourtId, string CourtNumber, string CourtName, string Status);

public sealed record ScheduleItemDto(
    Guid BookingId, 
    Guid CourtId,
    TimeOnly StartTime,
    TimeOnly EndTime,
    DateOnly BookingDate,
    string? BookingType, 
    string? BookingStatus,
    string? PaymentStatus, 
    string? CustomerName,
    string? PhoneNumber,
    string Title, 
    string Color
);

public sealed record CourtStatusResponse(
    Guid BranchId, 
    string BranchName,
    DateOnly Date,
    TimeOnly OpenTime,
    TimeOnly CloseTime,
    List<CourtDto> Courts,
    List<ScheduleItemDto> ScheduleItems
 );

public sealed record BookingDetailResponse(
    Guid BookingId, string BookingCode, string CourtName, string CustomerName, string PhoneNumber,
    DateOnly BookingDate, TimeOnly StartTime, TimeOnly EndTime, string BookingType,
    string BookingStatus, string PaymentStatus, decimal TotalAmount, decimal PaidAmount,
    decimal RemainingAmount);

public sealed record CreateBookingRequest(
    Guid BranchId, Guid CourtId, Guid CustomerId, DateOnly BookingDate, TimeOnly StartTime,
    TimeOnly EndTime, string BookingType, string? Note);

public sealed record UpdateBookingRequest(
    Guid CourtId, DateOnly BookingDate, TimeOnly StartTime, TimeOnly EndTime,
    string BookingType, string? Note);

public sealed record CreateCourtBlockRequest(
    Guid CourtId, DateOnly Date, TimeOnly StartTime, TimeOnly EndTime, string Reason);

public sealed record CourtBlockResponse(
    Guid Id, Guid CourtId, DateOnly Date, TimeOnly StartTime, TimeOnly EndTime, string Reason, string Color);

public sealed record CreateEventRequest(
    Guid BranchId, Guid? CourtId, DateOnly Date, TimeOnly StartTime, TimeOnly EndTime,
    string Name, string? Description);

public sealed record EventResponse(
    Guid Id, Guid? CourtId, DateOnly Date, TimeOnly StartTime, TimeOnly EndTime,
    string Name, string? Description, string Color);