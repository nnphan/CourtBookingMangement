namespace CourtBookingManagement.Application.Sales.Models.Responses;

public sealed record SalesReportResponse(
    DateOnly From,
    DateOnly To,
    long CompletedOrderCount,
    decimal GrossSales,
    decimal Refunds,
    decimal NetSales,
    IReadOnlyList<DailySalesResponse> DailySales,
    IReadOnlyList<TopSellingProductResponse> TopProducts);

public sealed record DailySalesResponse(DateOnly Date, long OrderCount, decimal GrossSales, decimal Refunds);

public sealed record TopSellingProductResponse(
    Guid ProductId,
    string Sku,
    string ProductName,
    decimal QuantitySold,
    decimal GrossSales);
