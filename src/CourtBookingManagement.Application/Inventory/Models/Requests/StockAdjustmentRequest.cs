namespace CourtBookingManagement.Application.Inventory.Models.Requests;

public sealed record StockAdjustmentRequest
{
    public required Guid ProductId { get; init; }

    public required string MovementType { get; init; }

    public required decimal Quantity { get; init; }

    public required string Notes { get; init; }
}
