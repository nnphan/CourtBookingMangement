namespace CourtBookingManagement.Application.Catalog.Models.Requests;

public sealed record CategorySearchRequest
{
    public string? Search { get; init; }

    public bool? IsActive { get; init; }
}
