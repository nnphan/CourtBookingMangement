namespace CourtBookingManagement.Application.Catalog.Models.Requests;

public sealed record ProductSearchRequest
{
    public string? Search { get; init; }

    public Guid? CategoryId { get; init; }

    public bool? IsActive { get; init; }

    public bool? TrackInventory { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 20;

    public string Sort { get; init; } = "name_asc";
}
