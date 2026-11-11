namespace CourtBookingManagement.Application.Inventory.Models.Responses;

public sealed record GoodsReceiptResponse(
    Guid Id,
    Guid BranchId,
    string ReceiptCode,
    string? SupplierName,
    string? SupplierReference,
    string Status,
    decimal TotalCost,
    string? Notes,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PostedAt,
    Guid? PostedBy)
{
    public IReadOnlyList<GoodsReceiptItemResponse>? Items { get; init; }
}

public sealed record GoodsReceiptItemResponse(
    Guid ProductId,
    string Sku,
    string ProductName,
    string Unit,
    decimal Quantity,
    decimal UnitCost,
    decimal LineTotal);
