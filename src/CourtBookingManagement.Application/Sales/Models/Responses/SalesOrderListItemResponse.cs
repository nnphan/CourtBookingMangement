namespace CourtBookingManagement.Application.Sales.Models.Responses;

public sealed record SalesOrderListItemResponse(
    Guid Id,
    Guid BranchId,
    string OrderCode,
    string? CustomerName,
    string Status,
    decimal TotalAmount,
    decimal PaidAmount,
    string PaymentMethodCode,
    string PaymentStatus,
    Guid? InvoiceId,
    string? InvoiceNumber,
    DateTimeOffset CreatedAt);
