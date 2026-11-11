namespace CourtBookingManagement.Application.Sales.Models.Requests;

public sealed record CheckoutRequest
{
    public Guid? CustomerId { get; init; }

    public Guid? BookingId { get; init; }

    public required string PaymentMethodCode { get; init; }

    public string? Notes { get; init; }

    public required IReadOnlyList<CheckoutItemRequest> Items { get; init; }
}

public sealed record CheckoutItemRequest
{
    public required Guid ProductId { get; init; }

    public required decimal Quantity { get; init; }
}
