using CourtBookingManagement.Api.Common.Responses;
using CourtBookingManagement.Application.Auth.Interfaces;
using CourtBookingManagement.Application.Inventory.Models.Requests;
using CourtBookingManagement.Application.Inventory.Models.Responses;
using CourtBookingManagement.Application.Inventory.Services;
using CourtBookingManagement.Application.Matching.Models;
using CourtBookingManagement.Infrastructure.Auth;
using Microsoft.AspNetCore.Mvc;

namespace CourtBookingManagement.Api.Controllers;

/// <summary>View and maintain branch-scoped inventory and goods receipts.</summary>
[ApiController]
[Route("api/branches/{branchId:guid}/inventory")]
public sealed class BranchInventoryController(
    IInventoryService service,
    ICurrentUserService currentUserService) : ApiControllerBase
{
    [HttpGet]
    [Permission("inventory.view")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<InventoryBalanceResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetBalances(
        Guid branchId,
        [FromQuery] InventoryBalanceSearchRequest request,
        CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not Guid userId)
        {
            return Unauthorized();
        }

        var result = await service.SearchBalancesAsync(userId, branchId, request, cancellationToken);
        return result.IsSuccess
            ? Success(result.Value.Items, "Branch inventory retrieved successfully.", result.Value.Metadata)
            : Error(result.Error);
    }

    [HttpGet("movements")]
    [Permission("inventory.view")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<StockMovementResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMovements(
        Guid branchId,
        [FromQuery] StockMovementSearchRequest request,
        CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not Guid userId)
        {
            return Unauthorized();
        }

        var result = await service.SearchMovementsAsync(userId, branchId, request, cancellationToken);
        return result.IsSuccess
            ? Success(result.Value.Items, "Stock movements retrieved successfully.", result.Value.Metadata)
            : Error(result.Error);
    }

    [HttpGet("goods-receipts")]
    [Permission("inventory.view")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<GoodsReceiptResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetReceipts(
        Guid branchId,
        [FromQuery] GoodsReceiptSearchRequest request,
        CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not Guid userId)
        {
            return Unauthorized();
        }

        var result = await service.SearchReceiptsAsync(userId, branchId, request, cancellationToken);
        return result.IsSuccess
            ? Success(result.Value.Items, "Goods receipts retrieved successfully.", result.Value.Metadata)
            : Error(result.Error);
    }

    [HttpGet("goods-receipts/{receiptId:guid}", Name = nameof(GetReceiptById))]
    [Permission("inventory.view")]
    [ProducesResponseType(typeof(ApiResponse<GoodsReceiptResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetReceiptById(
        Guid branchId,
        Guid receiptId,
        CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not Guid userId)
        {
            return Unauthorized();
        }

        var result = await service.GetReceiptAsync(userId, branchId, receiptId, cancellationToken);
        return FromResult(result, "Goods receipt retrieved successfully.");
    }

    [HttpPost("goods-receipts")]
    [Permission("inventory.receive")]
    [ProducesResponseType(typeof(ApiResponse<GoodsReceiptResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateReceipt(
        Guid branchId,
        [FromBody] GoodsReceiptDraftRequest request,
        CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not Guid userId)
        {
            return Unauthorized();
        }

        var result = await service.CreateReceiptDraftAsync(userId, branchId, request, cancellationToken);
        return result.IsSuccess
            ? Created(
                result.Value,
                nameof(GetReceiptById),
                new { branchId, receiptId = result.Value.Id },
                "Goods receipt draft created successfully.")
            : Error(result.Error);
    }

    [HttpPut("goods-receipts/{receiptId:guid}")]
    [Permission("inventory.receive")]
    [ProducesResponseType(typeof(ApiResponse<GoodsReceiptResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateReceipt(
        Guid branchId,
        Guid receiptId,
        [FromBody] GoodsReceiptDraftRequest request,
        CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not Guid userId)
        {
            return Unauthorized();
        }

        var result = await service.UpdateReceiptDraftAsync(
            userId,
            branchId,
            receiptId,
            request,
            cancellationToken);
        return FromResult(result, "Goods receipt draft updated successfully.");
    }

    [HttpPost("goods-receipts/{receiptId:guid}/post")]
    [Permission("inventory.post_receipt")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> PostReceipt(
        Guid branchId,
        Guid receiptId,
        CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not Guid userId)
        {
            return Unauthorized();
        }

        var result = await service.PostReceiptAsync(userId, branchId, receiptId, cancellationToken);
        return FromResult(result, "Goods receipt posted and stock updated successfully.");
    }

    [HttpPost("goods-receipts/{receiptId:guid}/cancel")]
    [Permission("inventory.receive")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CancelReceipt(
        Guid branchId,
        Guid receiptId,
        CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not Guid userId)
        {
            return Unauthorized();
        }

        var result = await service.CancelReceiptAsync(userId, branchId, receiptId, cancellationToken);
        return FromResult(result, "Goods receipt draft cancelled successfully.");
    }

    [HttpPost("adjustments")]
    [Permission("inventory.adjust")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AdjustStock(
        Guid branchId,
        [FromBody] StockAdjustmentRequest request,
        CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not Guid userId)
        {
            return Unauthorized();
        }

        var result = await service.AdjustStockAsync(userId, branchId, request, cancellationToken);
        return FromResult(result, "Inventory adjustment applied successfully.");
    }

    [HttpPut("{productId:guid}/reorder-level")]
    [Permission("inventory.adjust")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SetReorderLevel(
        Guid branchId,
        Guid productId,
        [FromBody] ReorderLevelRequest request,
        CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not Guid userId)
        {
            return Unauthorized();
        }

        var result = await service.SetReorderLevelAsync(
            userId,
            branchId,
            productId,
            request,
            cancellationToken);
        return FromResult(result, "Inventory reorder level updated successfully.");
    }
}
