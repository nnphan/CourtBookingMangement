using System.Net.Mime;
using CourtBookingManagement.Api.Common.Responses;
using CourtBookingManagement.Application.Auth.Interfaces;
using CourtBookingManagement.Application.Matching.Models;
using CourtBookingManagement.Application.Sales.Models.Requests;
using CourtBookingManagement.Application.Sales.Models.Responses;
using CourtBookingManagement.Application.Sales.Services;
using CourtBookingManagement.Infrastructure.Auth;
using Microsoft.AspNetCore.Mvc;

namespace CourtBookingManagement.Api.Controllers;

/// <summary>Checkout, query, cancel, and report branch-scoped POS sales.</summary>
[ApiController]
[Route("api/branches/{branchId:guid}/sales")]
public sealed class BranchSalesController(
    ISalesService service,
    ICurrentUserService currentUserService) : ApiControllerBase
{
    [HttpGet("orders")]
    [Permission("sales.view")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<SalesOrderListItemResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetOrders(
        Guid branchId,
        [FromQuery] SalesOrderSearchRequest request,
        CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not Guid userId)
        {
            return Unauthorized();
        }

        var result = await service.SearchOrdersAsync(userId, branchId, request, cancellationToken);
        return result.IsSuccess
            ? Success(result.Value.Items, "Sales orders retrieved successfully.", result.Value.Metadata)
            : Error(result.Error);
    }

    [HttpPost("orders", Name = nameof(Checkout))]
    [Permission("sales.create")]
    [ProducesResponseType(typeof(ApiResponse<SalesOrderResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Checkout(
        Guid branchId,
        [FromBody] CheckoutRequest request,
        CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not Guid userId)
        {
            return Unauthorized();
        }

        var result = await service.CheckoutAsync(userId, branchId, request, cancellationToken);
        return result.IsSuccess
            ? Created(
                result.Value,
                nameof(GetOrderById),
                new { branchId, orderId = result.Value.Id },
                "Sale completed successfully.")
            : Error(result.Error);
    }

    [HttpGet("orders/{orderId:guid}", Name = nameof(GetOrderById))]
    [Permission("sales.view")]
    [ProducesResponseType(typeof(ApiResponse<SalesOrderResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOrderById(
        Guid branchId,
        Guid orderId,
        CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not Guid userId)
        {
            return Unauthorized();
        }

        return FromResult(
            await service.GetOrderAsync(userId, branchId, orderId, cancellationToken),
            "Sales order retrieved successfully.");
    }

    [HttpPost("orders/{orderId:guid}/cancel")]
    [Permission("sales.cancel")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CancelOrder(
        Guid branchId,
        Guid orderId,
        [FromBody] CancelSalesOrderRequest request,
        CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not Guid userId)
        {
            return Unauthorized();
        }

        return FromResult(
            await service.CancelOrderAsync(userId, branchId, orderId, request, cancellationToken),
            "Sale cancelled and full refund recorded successfully.");
    }

    [HttpGet("report")]
    [Permission("sales.report.view")]
    [ProducesResponseType(typeof(ApiResponse<SalesReportResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetReport(
        Guid branchId,
        [FromQuery] SalesReportRequest request,
        CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not Guid userId)
        {
            return Unauthorized();
        }

        return FromResult(
            await service.GetReportAsync(userId, branchId, request, cancellationToken),
            "Sales report retrieved successfully.");
    }

    [HttpGet("invoices/{invoiceId:guid}")]
    [Permission("sales.invoice.view")]
    [ProducesResponseType(typeof(ApiResponse<InvoiceSnapshotResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetInvoice(
        Guid branchId,
        Guid invoiceId,
        CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not Guid userId)
        {
            return Unauthorized();
        }

        return FromResult(
            await service.GetInvoiceAsync(userId, branchId, invoiceId, cancellationToken),
            "Invoice snapshot retrieved successfully.");
    }

    [HttpGet("invoices/{invoiceId:guid}/preview")]
    [Permission("sales.invoice.view")]
    [Produces("application/pdf")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PreviewInvoice(
        Guid branchId,
        Guid invoiceId,
        CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not Guid userId)
        {
            return Unauthorized();
        }

        var result = await service.GetInvoicePdfAsync(userId, branchId, invoiceId, false, cancellationToken);
        if (result.IsFailure)
        {
            return Error(result.Error);
        }

        Response.Headers.ContentDisposition = $"inline; filename=\"invoice-{invoiceId:N}.pdf\"";
        return File(result.Value, MediaTypeNames.Application.Pdf);
    }

    [HttpGet("invoices/{invoiceId:guid}/download")]
    [Permission("sales.invoice.print")]
    [Produces("application/pdf")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadInvoice(
        Guid branchId,
        Guid invoiceId,
        CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not Guid userId)
        {
            return Unauthorized();
        }

        var result = await service.GetInvoicePdfAsync(userId, branchId, invoiceId, true, cancellationToken);
        return result.IsSuccess
            ? File(result.Value, MediaTypeNames.Application.Pdf, $"invoice-{invoiceId:N}.pdf")
            : Error(result.Error);
    }
}
