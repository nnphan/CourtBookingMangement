using CourtBookingManagement.Application.Inventory.Models.Requests;
using CourtBookingManagement.Application.Inventory.Models.Responses;
using CourtBookingManagement.Application.Matching.Models;

namespace CourtBookingManagement.Application.Inventory.Repositories;

public interface IInventoryRepository
{
    Task<PagedResult<InventoryBalanceResponse>> SearchBalancesAsync(
        Guid branchId,
        InventoryBalanceSearchRequest request,
        CancellationToken cancellationToken);

    Task<PagedResult<StockMovementResponse>> SearchMovementsAsync(
        Guid branchId,
        StockMovementSearchRequest request,
        CancellationToken cancellationToken);

    Task<PagedResult<GoodsReceiptResponse>> SearchReceiptsAsync(
        Guid branchId,
        GoodsReceiptSearchRequest request,
        CancellationToken cancellationToken);

    Task<GoodsReceiptResponse?> GetReceiptAsync(
        Guid branchId,
        Guid receiptId,
        CancellationToken cancellationToken);

    Task<InventoryOperationResult> CreateReceiptDraftAsync(
        Guid branchId,
        GoodsReceiptDraftRequest request,
        Guid actorId,
        CancellationToken cancellationToken);

    Task<InventoryOperationResult> UpdateReceiptDraftAsync(
        Guid branchId,
        Guid receiptId,
        GoodsReceiptDraftRequest request,
        Guid actorId,
        CancellationToken cancellationToken);

    Task<InventoryOperationResult> PostReceiptAsync(
        Guid branchId,
        Guid receiptId,
        Guid actorId,
        CancellationToken cancellationToken);

    Task<InventoryOperationResult> CancelReceiptAsync(
        Guid branchId,
        Guid receiptId,
        Guid actorId,
        CancellationToken cancellationToken);

    Task<InventoryOperationResult> AdjustStockAsync(
        Guid branchId,
        StockAdjustmentRequest request,
        Guid actorId,
        CancellationToken cancellationToken);

    Task<InventoryOperationResult> SetReorderLevelAsync(
        Guid branchId,
        Guid productId,
        decimal reorderLevel,
        Guid actorId,
        CancellationToken cancellationToken);
}

public sealed record InventoryOperationResult(InventoryOperationStatus Status, Guid? ResourceId = null);

public enum InventoryOperationStatus
{
    Succeeded,
    NotFound,
    InvalidState,
    ProductNotConfigured,
    InsufficientStock,
    QuantityLimitExceeded
}
