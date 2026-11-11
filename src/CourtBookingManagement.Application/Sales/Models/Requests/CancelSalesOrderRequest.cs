namespace CourtBookingManagement.Application.Sales.Models.Requests;

public sealed record CancelSalesOrderRequest
{
    public required string Reason { get; init; }
}
