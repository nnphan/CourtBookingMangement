namespace CourtBookingManagement.Application.CourtBookings.DTOs;

public sealed record CreateCourtBookingRequest
{
    public Guid BranchId { get; init; }
    public Guid CourtId { get; init; }
    public DateOnly BookingDate { get; init; }
    public TimeOnly StartTime { get; init; }
    public TimeOnly EndTime { get; init; }
    public string CustomerName { get; init; } = string.Empty;
    public string PhoneNumber { get; init; } = string.Empty;
    public string? Note { get; init; }
    public string PaymentMethod { get; init; } = "Cash";
}

public sealed record CreateCourtBookingResponse
{
    public Guid BookingId { get; init; }
    public string BookingCode { get; init; } = string.Empty;
    public string BookingStatus { get; init; } = string.Empty;
    public string CustomerName { get; init; } = string.Empty;
    public decimal TotalAmount { get; init; }
    public string PaymentMethod { get; init; } = "Cash";
    public DateOnly BookingDate { get; init; }
    public string BranchName { get; init; } = string.Empty;
    public TimeOnly StartTime { get; init; }
    public TimeOnly EndTime { get; init; }
    public DateTime CreatedAt { get; init; }
}

public sealed record CheckCourtAvailabilityRequest
{
    public Guid CourtId { get; init; }
    public DateOnly BookingDate { get; init; }
    public TimeOnly StartTime { get; init; }
    public TimeOnly EndTime { get; init; }
}

public sealed record CheckCourtAvailabilityResponse(bool Available);

public sealed record SearchCourtBookingsRequest
{
    public string PhoneNumber { get; init; } = string.Empty;
}

public sealed record CancelCourtBookingRequest
{
    public string BookingCode { get; init; } = string.Empty;
    public string PhoneNumber { get; init; } = string.Empty;
}