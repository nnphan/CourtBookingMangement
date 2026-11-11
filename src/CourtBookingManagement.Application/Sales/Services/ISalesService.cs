using CourtBookingManagement.Application.Matching.Models;
using CourtBookingManagement.Application.Sales.Models.Requests;
using CourtBookingManagement.Application.Sales.Models.Responses;
using CourtBookingManagement.Domain.Abstractions;

namespace CourtBookingManagement.Application.Sales.Services;

public interface ISalesService
{
    Task<Result<PagedResult<SalesOrderListItemResponse>>> SearchOrdersAsync(
        Guid userId,
        Guid branchId,
        SalesOrderSearchRequest request,
        CancellationToken cancellationToken);

    Task<Result<SalesOrderResponse>> GetOrderAsync(
        Guid userId,
        Guid branchId,
        Guid orderId,
        CancellationToken cancellationToken);

    Task<Result<SalesOrderResponse>> CheckoutAsync(
        Guid userId,
        Guid branchId,
        CheckoutRequest request,
        CancellationToken cancellationToken);

    Task<Result> CancelOrderAsync(
        Guid userId,
        Guid branchId,
        Guid orderId,
        CancelSalesOrderRequest request,
        CancellationToken cancellationToken);

    Task<Result<SalesReportResponse>> GetReportAsync(
        Guid userId,
        Guid branchId,
        SalesReportRequest request,
        CancellationToken cancellationToken);

    Task<Result<InvoiceSnapshotResponse>> GetInvoiceAsync(
        Guid userId,
        Guid branchId,
        Guid invoiceId,
        CancellationToken cancellationToken);

    Task<Result<byte[]>> GetInvoicePdfAsync(
        Guid userId,
        Guid branchId,
        Guid invoiceId,
        bool forPrint,
        CancellationToken cancellationToken);
}
