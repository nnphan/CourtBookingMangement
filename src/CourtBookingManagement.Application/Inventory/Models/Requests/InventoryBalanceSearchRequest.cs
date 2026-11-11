namespace CourtBookingManagement.Application.Inventory.Models.Requests;

public sealed record InventoryBalanceSearchRequest
{
    public string? Search { get; init; }

    public bool LowStockOnly { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 20;
}
