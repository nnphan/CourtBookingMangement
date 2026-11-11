namespace CourtBookingManagement.Application.Catalog.Models.Requests;

/// <summary>Fields used to add a product to the shared catalog.</summary>
public sealed record CreateProductRequest
{
    public required Guid CategoryId { get; init; }

    public required string Sku { get; init; }

    public string? Barcode { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    public required string Unit { get; init; }

    public decimal? DefaultCostPrice { get; init; }

    public required decimal DefaultSellingPrice { get; init; }

    public bool TrackInventory { get; init; } = true;

    public bool IsActive { get; init; } = true;
}
