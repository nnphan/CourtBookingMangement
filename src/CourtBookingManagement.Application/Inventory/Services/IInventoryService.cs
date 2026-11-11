using CourtBookingManagement.Application.Inventory.Models.Requests;
using CourtBookingManagement.Application.Inventory.Models.Responses;
using CourtBookingManagement.Application.Matching.Models;
using CourtBookingManagement.Domain.Abstractions;

namespace CourtBookingManagement.Application.Inventory.Services;

public interface IInventoryService
{
    Task<Result<PagedResult<InventoryBalanceResponse>>> SearchBalancesAsync(
        Guid userId,
        Guid branchId,
        InventoryBalanceSearchRequest request,
        CancellationToken cancellationToken);

    Task<Result<PagedResult<StockMovementResponse>>> SearchMovementsAsync(
        Guid userId,
        Guid branchId,
        StockMovementSearchRequest request,
        CancellationToken cancellationToken);

    Task<Result<PagedResult<GoodsReceiptResponse>>> SearchReceiptsAsync(
        Guid userId,
        Guid branchId,
        GoodsReceiptSearchRequest request,
        CancellationToken cancellationToken);

    Task<Result<GoodsReceiptResponse>> GetReceiptAsync(
        Guid userId,
        Guid branchId,
        Guid receiptId,
        CancellationToken cancellationToken);

    Task<Result<GoodsReceiptResponse>> CreateReceiptDraftAsync(
        Guid userId,
        Guid branchId,
        GoodsReceiptDraftRequest request,
        CancellationToken cancellationToken);

    Task<Result<GoodsReceiptResponse>> UpdateReceiptDraftAsync(
        Guid userId,
        Guid branchId,
        Guid receiptId,
        GoodsReceiptDraftRequest request,
        CancellationToken cancellationToken);

    Task<Result> PostReceiptAsync(
        Guid userId,
        Guid branchId,
        Guid receiptId,
        CancellationToken cancellationToken);

    Task<Result> CancelReceiptAsync(
        Guid userId,
        Guid branchId,
        Guid receiptId,
        CancellationToken cancellationToken);

    Task<Result> AdjustStockAsync(
        Guid userId,
        Guid branchId,
        StockAdjustmentRequest request,
        CancellationToken cancellationToken);

    Task<Result> SetReorderLevelAsync(
        Guid userId,
        Guid branchId,
        Guid productId,
        ReorderLevelRequest request,
        CancellationToken cancellationToken);
}
