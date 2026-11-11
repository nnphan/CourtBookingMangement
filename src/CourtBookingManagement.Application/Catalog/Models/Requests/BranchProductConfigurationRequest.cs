namespace CourtBookingManagement.Application.Catalog.Models.Requests;

/// <summary>Branch-specific sale availability and pricing for one product.</summary>
public sealed record BranchProductConfigurationRequest
{
    public required decimal SellingPrice { get; init; }

    public decimal? CostPrice { get; init; }

    public bool IsAvailableForSale { get; init; }

    public int DisplayOrder { get; init; }
}
