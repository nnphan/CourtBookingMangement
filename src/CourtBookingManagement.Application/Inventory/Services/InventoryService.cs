using CourtBookingManagement.Application.Auth.Interfaces;
using CourtBookingManagement.Application.Inventory.Models.Requests;
using CourtBookingManagement.Application.Inventory.Models.Responses;
using CourtBookingManagement.Application.Inventory.Repositories;
using CourtBookingManagement.Application.Matching.Models;
using CourtBookingManagement.Domain.Abstractions;
using FluentValidation;

namespace CourtBookingManagement.Application.Inventory.Services;

public sealed class InventoryService(
    IInventoryRepository repository,
    IBranchAuthorizationService branchAuthorizationService,
    IValidator<GoodsReceiptDraftRequest> goodsReceiptValidator,
    IValidator<StockAdjustmentRequest> stockAdjustmentValidator,
    IValidator<ReorderLevelRequest> reorderLevelValidator) : IInventoryService
{
    private static readonly Error InvalidRequest = new("Inventory.InvalidRequest", "The request is invalid.");
    private static readonly Error InvalidPagination = new("Inventory.InvalidPagination", "Page must be at least 1 and pageSize must be between 1 and 100.");
    private static readonly Error InvalidFilter = new("Inventory.InvalidFilter", "The inventory filter is invalid.");
    private static readonly Error ReceiptNotFound = new("Inventory.ReceiptNotFound", "Goods receipt not found.");
    private static readonly Error ReceiptNotDraft = new("Inventory.ReceiptNotDraft", "Only a draft goods receipt can be changed.");
    private static readonly Error ProductNotConfigured = new("Inventory.ProductNotConfigured", "Every receipt or adjustment product must be an active, inventory-tracked product configured for this branch.");
    private static readonly Error InsufficientStock = new("Inventory.InsufficientStock", "The adjustment would make branch inventory negative.");
    private static readonly Error QuantityLimitExceeded = new("Inventory.QuantityLimitExceeded", "The resulting inventory quantity exceeds the supported limit.");

    public async Task<Result<PagedResult<InventoryBalanceResponse>>> SearchBalancesAsync(
        Guid userId,
        Guid branchId,
        InventoryBalanceSearchRequest request,
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

        await branchAuthorizationService.EnsureCanAccessAsync(userId, branchId, "inventory.view", cancellationToken);
        return Result.Success(await repository.SearchBalancesAsync(branchId, Normalize(request), cancellationToken));
    }

    public async Task<Result<PagedResult<StockMovementResponse>>> SearchMovementsAsync(
        Guid userId,
        Guid branchId,
        StockMovementSearchRequest request,
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

        if (request.ProductId == Guid.Empty)
        {
            return InvalidFilter;
        }

        var movementType = NormalizeMovementType(request.MovementType);
        if (request.MovementType is not null && movementType is null)
        {
            return InvalidFilter;
        }

        await branchAuthorizationService.EnsureCanAccessAsync(userId, branchId, "inventory.view", cancellationToken);
        return Result.Success(await repository.SearchMovementsAsync(
            branchId,
            request with { MovementType = movementType },
            cancellationToken));
    }

    public async Task<Result<PagedResult<GoodsReceiptResponse>>> SearchReceiptsAsync(
        Guid userId,
        Guid branchId,
        GoodsReceiptSearchRequest request,
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

        var status = NormalizeReceiptStatus(request.Status);
        if (!string.IsNullOrWhiteSpace(request.Status) && status is null)
        {
            return InvalidFilter;
        }

        await branchAuthorizationService.EnsureCanAccessAsync(userId, branchId, "inventory.view", cancellationToken);
        return Result.Success(await repository.SearchReceiptsAsync(
            branchId,
            request with { Status = status, Search = NormalizeOptional(request.Search) },
            cancellationToken));
    }

    public async Task<Result<GoodsReceiptResponse>> GetReceiptAsync(
        Guid userId,
        Guid branchId,
        Guid receiptId,
        CancellationToken cancellationToken)
    {
        if (!IsValidIdentity(userId, branchId) || receiptId == Guid.Empty)
        {
            return InvalidRequest;
        }

        await branchAuthorizationService.EnsureCanAccessAsync(userId, branchId, "inventory.view", cancellationToken);
        var receipt = await repository.GetReceiptAsync(branchId, receiptId, cancellationToken);
        return receipt is null ? ReceiptNotFound : Result.Success(receipt);
    }

    public async Task<Result<GoodsReceiptResponse>> CreateReceiptDraftAsync(
        Guid userId,
        Guid branchId,
        GoodsReceiptDraftRequest request,
        CancellationToken cancellationToken)
    {
        await goodsReceiptValidator.ValidateAndThrowAsync(request, cancellationToken);
        if (!IsValidIdentity(userId, branchId))
        {
            return InvalidRequest;
        }

        await branchAuthorizationService.EnsureCanAccessAsync(userId, branchId, "inventory.receive", cancellationToken);
        var result = await repository.CreateReceiptDraftAsync(
            branchId,
            Normalize(request),
            userId,
            cancellationToken);
        if (result.Status != InventoryOperationStatus.Succeeded || result.ResourceId is not Guid receiptId)
        {
            return MapOperation<GoodsReceiptResponse>(result.Status);
        }

        var receipt = await repository.GetReceiptAsync(branchId, receiptId, cancellationToken);
        return receipt is null ? ReceiptNotFound : Result.Success(receipt);
    }

    public async Task<Result<GoodsReceiptResponse>> UpdateReceiptDraftAsync(
        Guid userId,
        Guid branchId,
        Guid receiptId,
        GoodsReceiptDraftRequest request,
        CancellationToken cancellationToken)
    {
        await goodsReceiptValidator.ValidateAndThrowAsync(request, cancellationToken);
        if (!IsValidIdentity(userId, branchId) || receiptId == Guid.Empty)
        {
            return InvalidRequest;
        }

        await branchAuthorizationService.EnsureCanAccessAsync(userId, branchId, "inventory.receive", cancellationToken);
        var result = await repository.UpdateReceiptDraftAsync(
            branchId,
            receiptId,
            Normalize(request),
            userId,
            cancellationToken);
        if (result.Status != InventoryOperationStatus.Succeeded)
        {
            return MapOperation<GoodsReceiptResponse>(result.Status);
        }

        var receipt = await repository.GetReceiptAsync(branchId, receiptId, cancellationToken);
        return receipt is null ? ReceiptNotFound : Result.Success(receipt);
    }

    public async Task<Result> PostReceiptAsync(
        Guid userId,
        Guid branchId,
        Guid receiptId,
        CancellationToken cancellationToken)
    {
        if (!IsValidIdentity(userId, branchId) || receiptId == Guid.Empty)
        {
            return Result.Failure(InvalidRequest);
        }

        await branchAuthorizationService.EnsureCanAccessAsync(userId, branchId, "inventory.post_receipt", cancellationToken);
        return MapOperation((await repository.PostReceiptAsync(branchId, receiptId, userId, cancellationToken)).Status);
    }

    public async Task<Result> CancelReceiptAsync(
        Guid userId,
        Guid branchId,
        Guid receiptId,
        CancellationToken cancellationToken)
    {
        if (!IsValidIdentity(userId, branchId) || receiptId == Guid.Empty)
        {
            return Result.Failure(InvalidRequest);
        }

        await branchAuthorizationService.EnsureCanAccessAsync(userId, branchId, "inventory.receive", cancellationToken);
        return MapOperation((await repository.CancelReceiptAsync(branchId, receiptId, userId, cancellationToken)).Status);
    }

    public async Task<Result> AdjustStockAsync(
        Guid userId,
        Guid branchId,
        StockAdjustmentRequest request,
        CancellationToken cancellationToken)
    {
        var normalized = request with
        {
            MovementType = request.MovementType?.Trim().ToUpperInvariant() ?? string.Empty,
            Notes = request.Notes?.Trim() ?? string.Empty
        };
        await stockAdjustmentValidator.ValidateAndThrowAsync(normalized, cancellationToken);
        if (!IsValidIdentity(userId, branchId))
        {
            return Result.Failure(InvalidRequest);
        }

        await branchAuthorizationService.EnsureCanAccessAsync(userId, branchId, "inventory.adjust", cancellationToken);
        return MapOperation((await repository.AdjustStockAsync(branchId, normalized, userId, cancellationToken)).Status);
    }

    public async Task<Result> SetReorderLevelAsync(
        Guid userId,
        Guid branchId,
        Guid productId,
        ReorderLevelRequest request,
        CancellationToken cancellationToken)
    {
        await reorderLevelValidator.ValidateAndThrowAsync(request, cancellationToken);
        if (!IsValidIdentity(userId, branchId) || productId == Guid.Empty)
        {
            return Result.Failure(InvalidRequest);
        }

        await branchAuthorizationService.EnsureCanAccessAsync(userId, branchId, "inventory.adjust", cancellationToken);
        return MapOperation((await repository.SetReorderLevelAsync(
            branchId,
            productId,
            request.ReorderLevel,
            userId,
            cancellationToken)).Status);
    }

    private static Result<T> MapOperation<T>(InventoryOperationStatus status) => status switch
    {
        InventoryOperationStatus.NotFound => Result.Failure<T>(ReceiptNotFound),
        InventoryOperationStatus.InvalidState => Result.Failure<T>(ReceiptNotDraft),
        InventoryOperationStatus.ProductNotConfigured => Result.Failure<T>(ProductNotConfigured),
        InventoryOperationStatus.InsufficientStock => Result.Failure<T>(InsufficientStock),
        InventoryOperationStatus.QuantityLimitExceeded => Result.Failure<T>(QuantityLimitExceeded),
        _ => Result.Failure<T>(InvalidRequest)
    };

    private static Result MapOperation(InventoryOperationStatus status) => status switch
    {
        InventoryOperationStatus.Succeeded => Result.Success(),
        InventoryOperationStatus.NotFound => Result.Failure(ReceiptNotFound),
        InventoryOperationStatus.InvalidState => Result.Failure(ReceiptNotDraft),
        InventoryOperationStatus.ProductNotConfigured => Result.Failure(ProductNotConfigured),
        InventoryOperationStatus.InsufficientStock => Result.Failure(InsufficientStock),
        InventoryOperationStatus.QuantityLimitExceeded => Result.Failure(QuantityLimitExceeded),
        _ => Result.Failure(InvalidRequest)
    };

    private static bool IsValidIdentity(Guid userId, Guid branchId) =>
        userId != Guid.Empty && branchId != Guid.Empty;

    private static bool IsValidPage(int page, int pageSize) =>
        page >= 1 && pageSize is >= 1 and <= 100;

    private static InventoryBalanceSearchRequest Normalize(InventoryBalanceSearchRequest request) =>
        request with { Search = NormalizeOptional(request.Search) };

    private static GoodsReceiptDraftRequest Normalize(GoodsReceiptDraftRequest request) =>
        request with
        {
            SupplierName = NormalizeOptional(request.SupplierName),
            SupplierReference = NormalizeOptional(request.SupplierReference),
            Notes = NormalizeOptional(request.Notes),
            Items = request.Items
                .Select(item => item with { UnitCost = decimal.Round(item.UnitCost, 2, MidpointRounding.AwayFromZero) })
                .ToArray()
        };

    private static string? NormalizeReceiptStatus(string? value) =>
        value?.Trim().ToUpperInvariant() switch
        {
            null or "" => null,
            "DRAFT" => "DRAFT",
            "POSTED" => "POSTED",
            "CANCELLED" => "CANCELLED",
            _ => null
        };

    private static string? NormalizeMovementType(string? value) =>
        value?.Trim().ToUpperInvariant() switch
        {
            null or "" => null,
            "OPENING_BALANCE" or "GOODS_RECEIPT" or "SALE" or "SALE_RETURN" or
                "ADJUSTMENT_IN" or "ADJUSTMENT_OUT" or "DAMAGE" or "TRANSFER_IN" or "TRANSFER_OUT" =>
                value.Trim().ToUpperInvariant(),
            _ => null
        };

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
