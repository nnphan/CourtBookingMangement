namespace CourtBookingManagement.Application.Matching.Models.Requests;

public sealed class MyMatchSearchRequest
{
    public string? Type { get; set; }

    public string? Status { get; set; }

    public DateOnly? MatchDate { get; set; }

    public string SortBy { get; set; } = "latest";

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 10;
}