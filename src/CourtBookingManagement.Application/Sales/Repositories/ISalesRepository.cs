using CourtBookingManagement.Application.Matching.Models;
using CourtBookingManagement.Application.Sales.Models;
using CourtBookingManagement.Application.Sales.Models.Requests;
using CourtBookingManagement.Application.Sales.Models.Responses;

namespace CourtBookingManagement.Application.Sales.Repositories;

public interface ISalesRepository
{
    Task<PagedResult<SalesOrderListItemResponse>> SearchOrdersAsync(
        Guid branchId,
        SalesOrderSearchRequest request,
        CancellationToken cancellationToken);

    Task<SalesOrderResponse?> GetOrderAsync(
        Guid branchId,
        Guid orderId,
        CancellationToken cancellationToken);

    Task<SalesOperationResult> CheckoutAsync(
        Guid branchId,
        CheckoutRequest request,
        Guid cashierId,
        CancellationToken cancellationToken);

    Task<SalesOperationResult> CancelOrderAsync(
        Guid branchId,
        Guid orderId,
        string reason,
        Guid actorId,
        CancellationToken cancellationToken);

    Task<InvoiceSnapshotResponse?> GetInvoiceAsync(
        Guid branchId,
        Guid invoiceId,
        CancellationToken cancellationToken);

    Task<SalesReportResponse> GetReportAsync(
        Guid branchId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken);
}
