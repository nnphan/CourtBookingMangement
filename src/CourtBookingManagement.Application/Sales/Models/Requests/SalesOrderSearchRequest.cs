namespace CourtBookingManagement.Application.Sales.Models.Requests;

public sealed record SalesOrderSearchRequest
{
    public string? Search { get; init; }

    public string? Status { get; init; }

    public DateTimeOffset? From { get; init; }

    public DateTimeOffset? To { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 20;
}
