namespace CourtBookingManagement.Application.Catalog.Models.Responses;

/// <summary>Product catalog data and its branch-specific sale configuration.</summary>
public sealed record BranchProductResponse(
    Guid ProductId,
    Guid CategoryId,
    string CategoryName,
    string Sku,
    string? Barcode,
    string Name,
    string? Description,
    string Unit,
    bool TrackInventory,
    decimal SellingPrice,
    decimal? CostPrice,
    bool IsAvailableForSale,
    int DisplayOrder,
    DateTimeOffset UpdatedAt);
