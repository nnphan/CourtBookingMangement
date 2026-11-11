namespace CourtBookingManagement.Application.Catalog.Models.Requests;

/// <summary>Branch-specific product configurations applied atomically.</summary>
public sealed record BulkBranchProductConfigurationRequest
{
    public required IReadOnlyList<BulkBranchProductConfigurationItem> Items { get; init; }
}

/// <summary>One product's branch-specific configuration in a bulk request.</summary>
public sealed record BulkBranchProductConfigurationItem
{
    public required Guid ProductId { get; init; }

    public required decimal SellingPrice { get; init; }

    public decimal? CostPrice { get; init; }

    public bool IsAvailableForSale { get; init; }

    public int DisplayOrder { get; init; }
}
