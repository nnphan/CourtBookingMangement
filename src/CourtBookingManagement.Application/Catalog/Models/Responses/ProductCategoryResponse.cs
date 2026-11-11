namespace CourtBookingManagement.Application.Catalog.Models.Responses;

/// <summary>Product category with audit timestamps.</summary>
public sealed record ProductCategoryResponse(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    int DisplayOrder,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
