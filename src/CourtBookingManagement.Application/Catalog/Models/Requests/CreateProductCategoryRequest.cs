namespace CourtBookingManagement.Application.Catalog.Models.Requests;

/// <summary>Fields used to create a shared product category.</summary>
public sealed record CreateProductCategoryRequest
{
    public required string Code { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    public int DisplayOrder { get; init; }

    public bool IsActive { get; init; } = true;
}
