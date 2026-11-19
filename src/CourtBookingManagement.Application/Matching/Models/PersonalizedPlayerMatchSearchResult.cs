using CourtBookingManagement.Application.Matching.Models.Responses;

namespace CourtBookingManagement.Application.Matching.Models;

public sealed class PersonalizedPlayerMatchSearchResult
{
    public PersonalizedPlayerMatchSearchResponse Data { get; set; } = new();

    public PagedResultMetadata Metadata { get; set; } = new(1, 20, 0, 0, false, false);
}
