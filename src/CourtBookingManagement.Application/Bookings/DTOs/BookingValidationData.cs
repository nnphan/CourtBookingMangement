namespace CourtBookingManagement.Application.Bookings.DTOs;

public sealed record BookingBranchData(
    Guid Id,
    bool IsActive,
    bool IsDeleted,
    DateOnly BusinessDate,
    string TimeZone,
    IReadOnlyList<BookingOperatingHourData> OperatingHours);

public sealed record BookingOperatingHourData(TimeOnly OpenTime, TimeOnly CloseTime, bool IsClosed);

public sealed record BookingCourtData(
    Guid Id,
    Guid BranchId,
    string CourtName,
    string Status,
    bool IsActive,
    bool IsDeleted);
