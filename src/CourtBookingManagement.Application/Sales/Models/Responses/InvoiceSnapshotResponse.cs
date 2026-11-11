namespace CourtBookingManagement.Application.Sales.Models.Responses;

public sealed record InvoiceSnapshotResponse(
    Guid Id,
    Guid BranchId,
    Guid SalesOrderId,
    string InvoiceNumber,
    string BranchName,
    string? BranchAddress,
    string? BranchPhone,
    string? CustomerName,
    string? CustomerPhone,
    string CashierName,
    decimal SubtotalAmount,
    decimal DiscountAmount,
    decimal TotalAmount,
    string PaymentMethodCode,
    string PaymentStatus,
    DateTimeOffset IssuedAt,
    IReadOnlyList<InvoiceSnapshotItemResponse> Items);

public sealed record InvoiceSnapshotItemResponse(
    string Sku,
    string ProductName,
    string Unit,
    decimal Quantity,
    decimal UnitPrice,
    decimal LineTotal);
