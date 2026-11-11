namespace CourtBookingManagement.Application.Inventory.Models.Requests;

public sealed record GoodsReceiptDraftRequest
{
    public string? SupplierName { get; init; }

    public string? SupplierReference { get; init; }

    public string? Notes { get; init; }

    public required IReadOnlyList<GoodsReceiptItemRequest> Items { get; init; }
}

public sealed record GoodsReceiptItemRequest
{
    public required Guid ProductId { get; init; }

    public required decimal Quantity { get; init; }

    public required decimal UnitCost { get; init; }
}
