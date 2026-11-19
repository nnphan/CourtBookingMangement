namespace CourtBookingManagement.Application.Matching.Models.Requests;

public sealed class PersonalizedPlayerMatchSearchRequest : PlayerMatchScoringCriteriaRequest
{
    public bool SaveAsDefault { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 20;
}
