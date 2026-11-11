namespace CourtBookingManagement.Application.Sales.Models.Responses;

public sealed record SalesOrderResponse(
    Guid Id,
    Guid BranchId,
    string OrderCode,
    Guid? CustomerId,
    string? CustomerName,
    Guid? BookingId,
    string Status,
    decimal SubtotalAmount,
    decimal DiscountAmount,
    decimal TotalAmount,
    decimal PaidAmount,
    string PaymentMethodCode,
    string PaymentStatus,
    string? Notes,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    Guid? InvoiceId,
    string? InvoiceNumber,
    IReadOnlyList<SalesOrderItemResponse> Items);

public sealed record SalesOrderItemResponse(
    Guid ProductId,
    string Sku,
    string ProductName,
    string Unit,
    decimal Quantity,
    decimal UnitPrice,
    decimal DiscountAmount,
    decimal LineTotal);
