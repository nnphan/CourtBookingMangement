namespace CourtBookingManagement.Application.Matching.Models.Requests;

public sealed class PlayerMatchSearchRequest
{
    public string? Keyword { get; set; }

    public DateOnly? MatchDate { get; set; }

    public TimeOnly? StartTime { get; set; }

    public TimeOnly? EndTime { get; set; }

    public string? SkillLevel { get; set; }

    public string? City { get; set; }

    public string? District { get; set; }

    public string SortBy { get; set; } = "latest";

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 9;
}
