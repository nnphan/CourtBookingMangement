namespace CourtBookingManagement.Application.Catalog.Models.Requests;

/// <summary>Fields used to update a product category.</summary>
public sealed record UpdateProductCategoryRequest
{
    public required string Code { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    public int DisplayOrder { get; init; }

    public bool IsActive { get; init; }
}
