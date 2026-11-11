namespace CourtBookingManagement.Application.Inventory.Models.Requests;

public sealed record GoodsReceiptSearchRequest
{
    public string? Status { get; init; }

    public string? Search { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 20;
}
