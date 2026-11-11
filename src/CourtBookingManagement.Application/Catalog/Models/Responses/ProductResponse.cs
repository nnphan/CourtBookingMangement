namespace CourtBookingManagement.Application.Catalog.Models.Responses;

/// <summary>Shared catalog product and its category.</summary>
public sealed record ProductResponse(
    Guid Id,
    Guid CategoryId,
    string CategoryCode,
    string CategoryName,
    string Sku,
    string? Barcode,
    string Name,
    string? Description,
    string Unit,
    decimal? DefaultCostPrice,
    decimal DefaultSellingPrice,
    bool TrackInventory,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
