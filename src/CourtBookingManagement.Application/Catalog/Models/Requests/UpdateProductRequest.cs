namespace CourtBookingManagement.Application.Catalog.Models.Requests;

/// <summary>Fields used to update a product in the shared catalog.</summary>
public sealed record UpdateProductRequest
{
    public required Guid CategoryId { get; init; }

    public required string Sku { get; init; }

    public string? Barcode { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    public required string Unit { get; init; }

    public decimal? DefaultCostPrice { get; init; }

    public required decimal DefaultSellingPrice { get; init; }

    public bool TrackInventory { get; init; }

    public bool IsActive { get; init; }
}
