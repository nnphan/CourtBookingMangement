namespace CourtBookingManagement.Application.Sales.Models.Requests;

public sealed record SalesReportRequest
{
    public required DateOnly From { get; init; }

    public required DateOnly To { get; init; }
}
