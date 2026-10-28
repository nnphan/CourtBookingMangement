namespace CourtBookingManagement.Application.Bookings.DTOs;

public sealed record BookingResponse
{
    public Guid Id { get; init; }
    public string BookingCode { get; init; } = string.Empty;
    public Guid CustomerId { get; init; }
    public Guid BranchId { get; init; }
    public string Status { get; init; } = string.Empty;
    public string BookingType { get; init; } = string.Empty;
    public decimal SubtotalAmount { get; init; }
    public decimal PaidAmount { get; init; }
    public decimal TotalAmount { get; init; }
    public string PaymentMethod { get; init; } = string.Empty;
    public string? Notes { get; init; }
    public int CourtCount { get; init; }
    public IReadOnlyList<BookingReservationResponse> Details { get; init; } = Array.Empty<BookingReservationResponse>();
}

public sealed record BookingReservationResponse
{
    public Guid Id { get; init; }
    public Guid CourtId { get; init; }
    public string CourtName { get; init; } = string.Empty;
    public DateOnly BookingDate { get; init; }
    public TimeOnly StartTime { get; init; }
    public TimeOnly EndTime { get; init; }
    public string Status { get; init; } = string.Empty;
    public decimal PriceCharged { get; init; }
}
