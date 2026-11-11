namespace CourtBookingManagement.Application.Inventory.Models.Responses;

public sealed record InventoryBalanceResponse(
    Guid ProductId,
    string Sku,
    string? Barcode,
    string ProductName,
    string CategoryName,
    string Unit,
    decimal QuantityOnHand,
    decimal QuantityReserved,
    decimal AvailableQuantity,
    decimal ReorderLevel,
    DateTimeOffset UpdatedAt);
