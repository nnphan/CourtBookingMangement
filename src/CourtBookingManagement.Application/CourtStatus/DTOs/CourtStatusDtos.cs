namespace CourtBookingManagement.Application.CourtStatus.DTOs;

public sealed record GetCourtStatusRequest
{
    public Guid BranchId { get; init; }

    public DateOnly Date { get; init; }
}

public enum ScheduleItemType { Booking, Block, Event }

public sealed record BranchDto(Guid Id, string Code, string Name);
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