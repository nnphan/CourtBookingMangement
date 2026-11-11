using CourtBookingManagement.Application.Auth.Interfaces;
using CourtBookingManagement.Application.Matching.Models;
using CourtBookingManagement.Application.Sales.Models;
using CourtBookingManagement.Application.Sales.Models.Requests;
using CourtBookingManagement.Application.Sales.Models.Responses;
using CourtBookingManagement.Application.Sales.Repositories;
using CourtBookingManagement.Domain.Abstractions;
using FluentValidation;

namespace CourtBookingManagement.Application.Sales.Services;

public sealed class SalesService(
    ISalesRepository repository,
    IBranchAuthorizationService branchAuthorizationService,
    IInvoicePdfGenerator invoicePdfGenerator,
    IValidator<CheckoutRequest> checkoutValidator,
    IValidator<CancelSalesOrderRequest> cancelValidator,
    IValidator<SalesReportRequest> reportValidator) : ISalesService
{
    private static readonly Error InvalidRequest = new("Sales.InvalidRequest", "The request is invalid.");
    private static readonly Error InvalidPagination = new("Sales.InvalidPagination", "Page must be at least 1 and pageSize must be between 1 and 100.");
    private static readonly Error InvalidFilter = new("Sales.InvalidFilter", "The sales filter is invalid.");
    private static readonly Error OrderNotFound = new("Sales.OrderNotFound", "Sales order not found.");
    private static readonly Error InvoiceNotFound = new("Sales.InvoiceNotFound", "Invoice not found.");
    private static readonly Error OrderNotCompletable = new("Sales.OrderNotCompletable", "The sales order cannot be completed.");
    private static readonly Error ProductNotAvailable = new("Sales.ProductNotAvailable", "A product is not active or available for sale at this branch.");
    private static readonly Error InsufficientStock = new("Sales.InsufficientStock", "There is insufficient available inventory for one or more products.");
    private static readonly Error QuantityLimitExceeded = new("Sales.QuantityLimitExceeded", "The resulting inventory quantity exceeds the supported limit.");
    private static readonly Error RelatedEntityNotFound = new("Sales.RelatedEntityNotFound", "The customer, booking, or payment method is not valid.");

    public async Task<Result<PagedResult<SalesOrderListItemResponse>>> SearchOrdersAsync(
        Guid userId,
        Guid branchId,
        SalesOrderSearchRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsValidIdentity(userId, branchId))
        {
            return InvalidRequest;
        }

        if (!IsValidPage(request.Page, request.PageSize))
        {
            return InvalidPagination;
        }

        if (request.From.HasValue && request.To.HasValue && request.From > request.To)
        {
            return InvalidFilter;
        }

        var status = NormalizeOrderStatus(request.Status);
        if (!string.IsNullOrWhiteSpace(request.Status) && status is null)
        {
            return InvalidFilter;
        }

        await branchAuthorizationService.EnsureCanAccessAsync(userId, branchId, "sales.view", cancellationToken);
        return Result.Success(await repository.SearchOrdersAsync(
            branchId,
            request with { Status = status, Search = NormalizeOptional(request.Search) },
            cancellationToken));
    }

    public async Task<Result<SalesOrderResponse>> GetOrderAsync(
        Guid userId,
        Guid branchId,
        Guid orderId,
        CancellationToken cancellationToken)
    {
        if (!IsValidIdentity(userId, branchId) || orderId == Guid.Empty)
        {
            return InvalidRequest;
        }

        await branchAuthorizationService.EnsureCanAccessAsync(userId, branchId, "sales.view", cancellationToken);
        var order = await repository.GetOrderAsync(branchId, orderId, cancellationToken);
        return order is null ? OrderNotFound : Result.Success(order);
    }

    public async Task<Result<SalesOrderResponse>> CheckoutAsync(
        Guid userId,
        Guid branchId,
        CheckoutRequest request,
        CancellationToken cancellationToken)
    {
        var normalized = request with
        {
            PaymentMethodCode = request.PaymentMethodCode?.Trim().ToUpperInvariant() ?? string.Empty,
            Notes = NormalizeOptional(request.Notes)
        };
        await checkoutValidator.ValidateAndThrowAsync(normalized, cancellationToken);
        if (!IsValidIdentity(userId, branchId))
        {
            return InvalidRequest;
        }

        await branchAuthorizationService.EnsureCanAccessAsync(userId, branchId, "sales.create", cancellationToken);
        var result = await repository.CheckoutAsync(branchId, normalized, userId, cancellationToken);
        if (result.Status != SalesOperationStatus.Succeeded || result.ResourceId is not Guid orderId)
        {
            return MapOperation<SalesOrderResponse>(result.Status);
        }

        var order = await repository.GetOrderAsync(branchId, orderId, cancellationToken);
        return order is null ? OrderNotFound : Result.Success(order);
    }

    public async Task<Result> CancelOrderAsync(
        Guid userId,
        Guid branchId,
        Guid orderId,
        CancelSalesOrderRequest request,
        CancellationToken cancellationToken)
    {
        var normalized = request with { Reason = request.Reason?.Trim() ?? string.Empty };
        await cancelValidator.ValidateAndThrowAsync(normalized, cancellationToken);
        if (!IsValidIdentity(userId, branchId) || orderId == Guid.Empty)
        {
            return Result.Failure(InvalidRequest);
        }

        await branchAuthorizationService.EnsureCanAccessAsync(userId, branchId, "sales.cancel", cancellationToken);
        await branchAuthorizationService.EnsureCanAccessAsync(userId, branchId, "sales.refund", cancellationToken);
        return MapOperation((await repository.CancelOrderAsync(
            branchId,
            orderId,
            normalized.Reason,
            userId,
            cancellationToken)).Status);
    }

    public async Task<Result<SalesReportResponse>> GetReportAsync(
        Guid userId,
        Guid branchId,
        SalesReportRequest request,
        CancellationToken cancellationToken)
    {
        await reportValidator.ValidateAndThrowAsync(request, cancellationToken);
        if (!IsValidIdentity(userId, branchId))
        {
            return InvalidRequest;
        }

        await branchAuthorizationService.EnsureCanAccessAsync(userId, branchId, "sales.report.view", cancellationToken);
        return Result.Success(await repository.GetReportAsync(branchId, request.From, request.To, cancellationToken));
    }

    public async Task<Result<InvoiceSnapshotResponse>> GetInvoiceAsync(
        Guid userId,
        Guid branchId,
        Guid invoiceId,
        CancellationToken cancellationToken)
    {
        if (!IsValidIdentity(userId, branchId) || invoiceId == Guid.Empty)
        {
            return InvalidRequest;
        }

        await branchAuthorizationService.EnsureCanAccessAsync(userId, branchId, "sales.invoice.view", cancellationToken);
        var invoice = await repository.GetInvoiceAsync(branchId, invoiceId, cancellationToken);
        return invoice is null ? InvoiceNotFound : Result.Success(invoice);
    }

    public async Task<Result<byte[]>> GetInvoicePdfAsync(
        Guid userId,
        Guid branchId,
        Guid invoiceId,
        bool forPrint,
        CancellationToken cancellationToken)
    {
        if (!IsValidIdentity(userId, branchId) || invoiceId == Guid.Empty)
        {
            return InvalidRequest;
        }

        await branchAuthorizationService.EnsureCanAccessAsync(
            userId,
            branchId,
            forPrint ? "sales.invoice.print" : "sales.invoice.view",
            cancellationToken);
        var invoice = await repository.GetInvoiceAsync(branchId, invoiceId, cancellationToken);
        return invoice is null
            ? Result.Failure<byte[]>(InvoiceNotFound)
            : Result.Success(invoicePdfGenerator.Generate(invoice));
    }

    private static Result<T> MapOperation<T>(SalesOperationStatus status) => status switch
    {
        SalesOperationStatus.NotFound => Result.Failure<T>(OrderNotFound),
        SalesOperationStatus.InvalidState => Result.Failure<T>(OrderNotCompletable),
        SalesOperationStatus.ProductNotConfigured => Result.Failure<T>(ProductNotAvailable),
        SalesOperationStatus.InsufficientStock => Result.Failure<T>(InsufficientStock),
        SalesOperationStatus.QuantityLimitExceeded => Result.Failure<T>(QuantityLimitExceeded),
        SalesOperationStatus.RelatedEntityNotFound => Result.Failure<T>(RelatedEntityNotFound),
        _ => Result.Failure<T>(InvalidRequest)
    };

    private static Result MapOperation(SalesOperationStatus status) => status switch
    {
        SalesOperationStatus.Succeeded => Result.Success(),
        SalesOperationStatus.NotFound => Result.Failure(OrderNotFound),
        SalesOperationStatus.InvalidState => Result.Failure(OrderNotCompletable),
        SalesOperationStatus.ProductNotConfigured => Result.Failure(ProductNotAvailable),
        SalesOperationStatus.InsufficientStock => Result.Failure(InsufficientStock),
        SalesOperationStatus.QuantityLimitExceeded => Result.Failure(QuantityLimitExceeded),
        SalesOperationStatus.RelatedEntityNotFound => Result.Failure(RelatedEntityNotFound),
        _ => Result.Failure(InvalidRequest)
    };

    private static bool IsValidIdentity(Guid userId, Guid branchId) =>
        userId != Guid.Empty && branchId != Guid.Empty;

    private static bool IsValidPage(int page, int pageSize) =>
        page >= 1 && pageSize is >= 1 and <= 100;

    private static string? NormalizeOrderStatus(string? value) =>
        value?.Trim().ToUpperInvariant() switch
        {
            null or "" => null,
            "COMPLETED" or "CANCELLED" => value.Trim().ToUpperInvariant(),
            _ => null
        };

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
