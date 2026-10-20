namespace CourtBookingManagement.Application.Matching.Models.Requests;

public sealed class GetHostedMatchesRequest
{
    public string? Keyword { get; set; }

    public DateOnly? MatchDate { get; set; }

    public string? Status { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 10;
}
