namespace CourtBookingManagement.Application.Matching.Models.Requests;

public sealed class GetParticipatingMatchesRequest
{
    public string? ParticipationStatus { get; set; }

    public string? MatchStatus { get; set; }

    public DateOnly? FromDate { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 10;
}
