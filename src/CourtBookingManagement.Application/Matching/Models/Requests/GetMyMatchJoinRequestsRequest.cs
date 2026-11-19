namespace CourtBookingManagement.Application.Matching.Models.Requests;

public sealed class GetMyMatchJoinRequestsRequest
{
    public string? Status { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 10;
}
