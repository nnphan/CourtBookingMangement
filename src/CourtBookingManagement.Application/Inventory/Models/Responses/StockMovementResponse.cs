namespace CourtBookingManagement.Application.Inventory.Models.Responses;

public sealed record StockMovementResponse(
    Guid Id,
    Guid ProductId,
    string Sku,
    string ProductName,
    string MovementType,
    decimal Quantity,
    decimal QuantityBefore,
    decimal QuantityAfter,
    string? ReferenceType,
    Guid? ReferenceId,
    string? Notes,
    DateTimeOffset CreatedAt,
    Guid? CreatedBy);
