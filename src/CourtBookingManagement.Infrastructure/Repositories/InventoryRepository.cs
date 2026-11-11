using System.Data;
using CourtBookingManagement.Application.Abstractions.Data;
using CourtBookingManagement.Application.Inventory.Models.Requests;
using CourtBookingManagement.Application.Inventory.Models.Responses;
using CourtBookingManagement.Application.Inventory.Repositories;
using CourtBookingManagement.Application.Matching.Models;
using Dapper;

namespace CourtBookingManagement.Infrastructure.Repositories;

public sealed class InventoryRepository(ISqlConnectionFactory connectionFactory) : IInventoryRepository
{
    public async Task<PagedResult<InventoryBalanceResponse>> SearchBalancesAsync(
        Guid branchId,
        InventoryBalanceSearchRequest request,
        CancellationToken cancellationToken)
    {
        const string filterSql = """
            FROM catalog.branch_products bp
            JOIN catalog.products p ON p.id = bp.product_id
            JOIN catalog.product_categories c ON c.id = p.category_id
            LEFT JOIN inventory.branch_inventory bi
                ON bi.branch_id = bp.branch_id AND bi.product_id = bp.product_id
            WHERE bp.branch_id = @BranchId
              AND p.track_inventory
              AND p.is_active
              AND p.deleted_at IS NULL
              AND c.is_active
              AND c.deleted_at IS NULL
              AND (@Search IS NULL OR p.name ILIKE @Search OR p.sku ILIKE @Search OR p.barcode ILIKE @Search)
              AND (NOT @LowStockOnly OR COALESCE(bi.quantity_on_hand, 0) <= COALESCE(bi.reorder_level, 0))
            """;
        var parameters = new
        {
            BranchId = branchId,
            Search = string.IsNullOrWhiteSpace(request.Search) ? null : $"%{request.Search}%",
            request.LowStockOnly,
            request.PageSize,
            Offset = (long)(request.Page - 1) * request.PageSize
        };
        const string projection = """
            SELECT
                p.id AS ProductId,
                p.sku AS Sku,
                p.barcode AS Barcode,
                p.name AS ProductName,
                c.name AS CategoryName,
                p.unit AS Unit,
                COALESCE(bi.quantity_on_hand, 0) AS QuantityOnHand,
                COALESCE(bi.quantity_reserved, 0) AS QuantityReserved,
                COALESCE(bi.quantity_on_hand, 0) - COALESCE(bi.quantity_reserved, 0) AS AvailableQuantity,
                COALESCE(bi.reorder_level, 0) AS ReorderLevel,
                COALESCE(bi.updated_at, bp.updated_at) AS UpdatedAt
            """;

        using var connection = connectionFactory.CreateConnection();
        var items = (await connection.QueryAsync<InventoryBalanceResponse>(new CommandDefinition(
            projection + filterSql + " ORDER BY c.name, p.name, p.id LIMIT @PageSize OFFSET @Offset;",
            parameters,
            cancellationToken: cancellationToken))).AsList();
        var count = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            "SELECT COUNT(*) " + filterSql,
            parameters,
            cancellationToken: cancellationToken));
        return PagedResult<InventoryBalanceResponse>.Create(items, request.Page, request.PageSize, count);
    }

    public async Task<PagedResult<StockMovementResponse>> SearchMovementsAsync(
        Guid branchId,
        StockMovementSearchRequest request,
        CancellationToken cancellationToken)
    {
        const string filterSql = """
            FROM inventory.stock_movements sm
            JOIN catalog.products p ON p.id = sm.product_id
            WHERE sm.branch_id = @BranchId
              AND (@ProductId::uuid IS NULL OR sm.product_id = @ProductId)
              AND (@MovementType IS NULL OR sm.movement_type = @MovementType)
              AND (@From::timestamptz IS NULL OR sm.created_at >= @From)
              AND (@To::timestamptz IS NULL OR sm.created_at <= @To)
            """;
        var parameters = new
        {
            BranchId = branchId,
            request.ProductId,
            request.MovementType,
            request.From,
            request.To,
            request.PageSize,
            Offset = (long)(request.Page - 1) * request.PageSize
        };
        const string projection = """
            SELECT
                sm.id AS Id,
                sm.product_id AS ProductId,
                p.sku AS Sku,
                p.name AS ProductName,
                sm.movement_type AS MovementType,
                sm.quantity AS Quantity,
                sm.quantity_before AS QuantityBefore,
                sm.quantity_after AS QuantityAfter,
                sm.reference_type AS ReferenceType,
                sm.reference_id AS ReferenceId,
                sm.notes AS Notes,
                sm.created_at AS CreatedAt,
                sm.created_by AS CreatedBy
            """;

        using var connection = connectionFactory.CreateConnection();
        var items = (await connection.QueryAsync<StockMovementResponse>(new CommandDefinition(
            projection + filterSql + " ORDER BY sm.created_at DESC, sm.id DESC LIMIT @PageSize OFFSET @Offset;",
            parameters,
            cancellationToken: cancellationToken))).AsList();
        var count = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            "SELECT COUNT(*) " + filterSql,
            parameters,
            cancellationToken: cancellationToken));
        return PagedResult<StockMovementResponse>.Create(items, request.Page, request.PageSize, count);
    }

    public async Task<PagedResult<GoodsReceiptResponse>> SearchReceiptsAsync(
        Guid branchId,
        GoodsReceiptSearchRequest request,
        CancellationToken cancellationToken)
    {
        const string filterSql = """
            FROM inventory.goods_receipts gr
            WHERE gr.branch_id = @BranchId
              AND (@Status IS NULL OR gr.status = @Status)
              AND (@Search IS NULL OR gr.receipt_code ILIKE @Search
                   OR gr.supplier_name ILIKE @Search OR gr.supplier_reference ILIKE @Search)
            """;
        var parameters = new
        {
            BranchId = branchId,
            request.Status,
            Search = string.IsNullOrWhiteSpace(request.Search) ? null : $"%{request.Search}%",
            request.PageSize,
            Offset = (long)(request.Page - 1) * request.PageSize
        };
        const string projection = """
            SELECT
                gr.id AS Id,
                gr.branch_id AS BranchId,
                gr.receipt_code AS ReceiptCode,
                gr.supplier_name AS SupplierName,
                gr.supplier_reference AS SupplierReference,
                gr.status AS Status,
                gr.total_cost AS TotalCost,
                gr.notes AS Notes,
                gr.created_at AS CreatedAt,
                gr.posted_at AS PostedAt,
                gr.posted_by AS PostedBy
            """;

        using var connection = connectionFactory.CreateConnection();
        var items = (await connection.QueryAsync<GoodsReceiptResponse>(new CommandDefinition(
            projection + filterSql + " ORDER BY gr.created_at DESC, gr.id DESC LIMIT @PageSize OFFSET @Offset;",
            parameters,
            cancellationToken: cancellationToken))).AsList();
        var count = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            "SELECT COUNT(*) " + filterSql,
            parameters,
            cancellationToken: cancellationToken));
        return PagedResult<GoodsReceiptResponse>.Create(items, request.Page, request.PageSize, count);
    }

    public async Task<GoodsReceiptResponse?> GetReceiptAsync(
        Guid branchId,
        Guid receiptId,
        CancellationToken cancellationToken)
    {
        const string headerSql = """
            SELECT
                gr.id AS Id,
                gr.branch_id AS BranchId,
                gr.receipt_code AS ReceiptCode,
                gr.supplier_name AS SupplierName,
                gr.supplier_reference AS SupplierReference,
                gr.status AS Status,
                gr.total_cost AS TotalCost,
                gr.notes AS Notes,
                gr.created_at AS CreatedAt,
                gr.posted_at AS PostedAt,
                gr.posted_by AS PostedBy
            FROM inventory.goods_receipts gr
            WHERE gr.branch_id = @BranchId AND gr.id = @ReceiptId;
            """;
        using var connection = connectionFactory.CreateConnection();
        var receipt = await connection.QuerySingleOrDefaultAsync<GoodsReceiptResponse>(new CommandDefinition(
            headerSql,
            new { BranchId = branchId, ReceiptId = receiptId },
            cancellationToken: cancellationToken));
        if (receipt is null)
        {
            return null;
        }

        var items = await connection.QueryAsync<GoodsReceiptItemResponse>(new CommandDefinition("""
            SELECT
                gri.product_id AS ProductId,
                p.sku AS Sku,
                p.name AS ProductName,
                p.unit AS Unit,
                gri.quantity AS Quantity,
                gri.unit_cost AS UnitCost,
                gri.line_total AS LineTotal
            FROM inventory.goods_receipt_items gri
            JOIN catalog.products p ON p.id = gri.product_id
            WHERE gri.goods_receipt_id = @ReceiptId
            ORDER BY p.name, p.id;
            """,
            new { ReceiptId = receiptId },
            cancellationToken: cancellationToken));
        return receipt with { Items = items.AsList() };
    }

    public async Task<InventoryOperationResult> CreateReceiptDraftAsync(
        Guid branchId,
        GoodsReceiptDraftRequest request,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);
        try
        {
            if (!await ReceiptProductsAreConfiguredAsync(connection, transaction, branchId, request.Items, cancellationToken))
            {
                transaction.Rollback();
                return new(InventoryOperationStatus.ProductNotConfigured);
            }

            var receiptId = Guid.NewGuid();
            var receiptCode = $"GR-{DateTimeOffset.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}";
            var total = CalculateTotal(request.Items);
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO inventory.goods_receipts
                    (id, receipt_code, branch_id, supplier_name, supplier_reference, status,
                     total_cost, notes, created_by, updated_by)
                VALUES
                    (@Id, @ReceiptCode, @BranchId, @SupplierName, @SupplierReference, 'DRAFT',
                     @TotalCost, @Notes, @ActorId, @ActorId);
                """,
                new
                {
                    Id = receiptId,
                    ReceiptCode = receiptCode,
                    BranchId = branchId,
                    request.SupplierName,
                    request.SupplierReference,
                    TotalCost = total,
                    request.Notes,
                    ActorId = actorId
                },
                transaction,
                cancellationToken: cancellationToken));
            await InsertReceiptItemsAsync(connection, transaction, receiptId, request.Items, cancellationToken);
            transaction.Commit();
            return new(InventoryOperationStatus.Succeeded, receiptId);
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<InventoryOperationResult> UpdateReceiptDraftAsync(
        Guid branchId,
        Guid receiptId,
        GoodsReceiptDraftRequest request,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);
        try
        {
            var status = await connection.QuerySingleOrDefaultAsync<string>(new CommandDefinition("""
                SELECT status
                FROM inventory.goods_receipts
                WHERE id = @ReceiptId AND branch_id = @BranchId
                FOR UPDATE;
                """,
                new { ReceiptId = receiptId, BranchId = branchId },
                transaction,
                cancellationToken: cancellationToken));
            if (status is null)
            {
                transaction.Rollback();
                return new(InventoryOperationStatus.NotFound);
            }

            if (status != "DRAFT")
            {
                transaction.Rollback();
                return new(InventoryOperationStatus.InvalidState);
            }

            if (!await ReceiptProductsAreConfiguredAsync(connection, transaction, branchId, request.Items, cancellationToken))
            {
                transaction.Rollback();
                return new(InventoryOperationStatus.ProductNotConfigured);
            }

            var updated = await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE inventory.goods_receipts
                SET supplier_name = @SupplierName,
                    supplier_reference = @SupplierReference,
                    total_cost = @TotalCost,
                    notes = @Notes,
                    updated_at = now(),
                    updated_by = @ActorId
                WHERE id = @ReceiptId AND branch_id = @BranchId AND status = 'DRAFT';
                """,
                new
                {
                    ReceiptId = receiptId,
                    BranchId = branchId,
                    request.SupplierName,
                    request.SupplierReference,
                    TotalCost = CalculateTotal(request.Items),
                    request.Notes,
                    ActorId = actorId
                },
                transaction,
                cancellationToken: cancellationToken));
            if (updated != 1)
            {
                throw new InvalidOperationException("The locked goods receipt draft was not updated.");
            }

            await connection.ExecuteAsync(new CommandDefinition(
                "DELETE FROM inventory.goods_receipt_items WHERE goods_receipt_id = @ReceiptId;",
                new { ReceiptId = receiptId },
                transaction,
                cancellationToken: cancellationToken));
            await InsertReceiptItemsAsync(connection, transaction, receiptId, request.Items, cancellationToken);
            transaction.Commit();
            return new(InventoryOperationStatus.Succeeded, receiptId);
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<InventoryOperationResult> PostReceiptAsync(
        Guid branchId,
        Guid receiptId,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);
        try
        {
            var status = await connection.QuerySingleOrDefaultAsync<string>(new CommandDefinition("""
                SELECT status
                FROM inventory.goods_receipts
                WHERE id = @ReceiptId AND branch_id = @BranchId
                FOR UPDATE;
                """,
                new { ReceiptId = receiptId, BranchId = branchId },
                transaction,
                cancellationToken: cancellationToken));
            if (status is null)
            {
                transaction.Rollback();
                return new(InventoryOperationStatus.NotFound);
            }

            if (status != "DRAFT")
            {
                transaction.Rollback();
                return new(InventoryOperationStatus.InvalidState);
            }

            var items = (await connection.QueryAsync<ReceiptPostingItem>(new CommandDefinition("""
                SELECT product_id AS ProductId, quantity AS Quantity
                FROM inventory.goods_receipt_items
                WHERE goods_receipt_id = @ReceiptId
                ORDER BY product_id;
                """,
                new { ReceiptId = receiptId },
                transaction,
                cancellationToken: cancellationToken))).AsList();
            if (items.Count == 0)
            {
                transaction.Rollback();
                return new(InventoryOperationStatus.InvalidState);
            }

            if (!await ReceiptProductsAreConfiguredAsync(
                    connection,
                    transaction,
                    branchId,
                    items.Select(item => item.ProductId).ToArray(),
                    cancellationToken))
            {
                transaction.Rollback();
                return new(InventoryOperationStatus.ProductNotConfigured);
            }

            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO inventory.branch_inventory
                    (branch_id, product_id, quantity_on_hand, quantity_reserved, reorder_level)
                SELECT @BranchId, p.id, 0, 0, 0
                FROM catalog.products p
                JOIN catalog.branch_products bp ON bp.product_id = p.id AND bp.branch_id = @BranchId
                WHERE p.id = ANY(@ProductIds) AND p.track_inventory
                ON CONFLICT (branch_id, product_id) DO NOTHING;
                """,
                new { BranchId = branchId, ProductIds = items.Select(item => item.ProductId).ToArray() },
                transaction,
                cancellationToken: cancellationToken));

            var balances = (await connection.QueryAsync<LockedInventoryBalance>(new CommandDefinition("""
                SELECT
                    product_id AS ProductId,
                    quantity_on_hand AS QuantityOnHand,
                    quantity_reserved AS QuantityReserved
                FROM inventory.branch_inventory
                WHERE branch_id = @BranchId AND product_id = ANY(@ProductIds)
                ORDER BY product_id
                FOR UPDATE;
                """,
                new { BranchId = branchId, ProductIds = items.Select(item => item.ProductId).ToArray() },
                transaction,
                cancellationToken: cancellationToken))).ToDictionary(balance => balance.ProductId);
            if (balances.Count != items.Count)
            {
                transaction.Rollback();
                return new(InventoryOperationStatus.ProductNotConfigured);
            }

            foreach (var item in items)
            {
                var before = balances[item.ProductId].QuantityOnHand;
                var after = before + item.Quantity;
                if (after > 999_999_999.999m)
                {
                    transaction.Rollback();
                    return new(InventoryOperationStatus.QuantityLimitExceeded);
                }

                var updated = await connection.ExecuteAsync(new CommandDefinition("""
                    UPDATE inventory.branch_inventory
                    SET quantity_on_hand = @After, updated_at = now(), updated_by = @ActorId
                    WHERE branch_id = @BranchId AND product_id = @ProductId;
                    """,
                    new
                    {
                        BranchId = branchId,
                        item.ProductId,
                        After = after,
                        ActorId = actorId
                    },
                    transaction,
                    cancellationToken: cancellationToken));
                if (updated != 1)
                {
                    throw new InvalidOperationException("The locked inventory row was not updated while posting the receipt.");
                }

                await connection.ExecuteAsync(new CommandDefinition("""
                    INSERT INTO inventory.stock_movements
                        (branch_id, product_id, movement_type, quantity, quantity_before, quantity_after,
                         reference_type, reference_id, notes, created_by)
                    VALUES
                        (@BranchId, @ProductId, 'GOODS_RECEIPT', @Quantity, @Before, @After,
                         'GOODS_RECEIPT', @ReceiptId, 'Goods receipt posted', @ActorId);
                    """,
                    new
                    {
                        BranchId = branchId,
                        item.ProductId,
                        item.Quantity,
                        Before = before,
                        After = after,
                        ReceiptId = receiptId,
                        ActorId = actorId
                    },
                    transaction,
                    cancellationToken: cancellationToken));
            }

            var receiptUpdated = await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE inventory.goods_receipts
                SET status = 'POSTED',
                    received_at = now(),
                    received_by = @ActorId,
                    posted_at = now(),
                    posted_by = @ActorId,
                    updated_at = now(),
                    updated_by = @ActorId
                WHERE id = @ReceiptId AND branch_id = @BranchId AND status = 'DRAFT';
                """,
                new { ReceiptId = receiptId, BranchId = branchId, ActorId = actorId },
                transaction,
                cancellationToken: cancellationToken));
            if (receiptUpdated != 1)
            {
                throw new InvalidOperationException("The locked goods receipt was not updated while posting.");
            }

            transaction.Commit();
            return new(InventoryOperationStatus.Succeeded, receiptId);
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<InventoryOperationResult> CancelReceiptAsync(
        Guid branchId,
        Guid receiptId,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);
        try
        {
            var status = await connection.QuerySingleOrDefaultAsync<string>(new CommandDefinition("""
                SELECT status
                FROM inventory.goods_receipts
                WHERE id = @ReceiptId AND branch_id = @BranchId
                FOR UPDATE;
                """,
                new { ReceiptId = receiptId, BranchId = branchId },
                transaction,
                cancellationToken: cancellationToken));
            if (status is null)
            {
                transaction.Rollback();
                return new(InventoryOperationStatus.NotFound);
            }

            if (status != "DRAFT")
            {
                transaction.Rollback();
                return new(InventoryOperationStatus.InvalidState);
            }

            var receiptUpdated = await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE inventory.goods_receipts
                SET status = 'CANCELLED',
                    cancelled_at = now(),
                    cancelled_by = @ActorId,
                    updated_at = now(),
                    updated_by = @ActorId
                WHERE id = @ReceiptId AND branch_id = @BranchId AND status = 'DRAFT';
                """,
                new { ReceiptId = receiptId, BranchId = branchId, ActorId = actorId },
                transaction,
                cancellationToken: cancellationToken));
            if (receiptUpdated != 1)
            {
                throw new InvalidOperationException("The locked goods receipt draft was not cancelled.");
            }

            transaction.Commit();
            return new(InventoryOperationStatus.Succeeded, receiptId);
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<InventoryOperationResult> AdjustStockAsync(
        Guid branchId,
        StockAdjustmentRequest request,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);
        try
        {
            if (!await ReceiptProductsAreConfiguredAsync(
                    connection,
                    transaction,
                    branchId,
                    [request.ProductId],
                    cancellationToken))
            {
                transaction.Rollback();
                return new(InventoryOperationStatus.ProductNotConfigured);
            }

            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO inventory.branch_inventory
                    (branch_id, product_id, quantity_on_hand, quantity_reserved, reorder_level)
                SELECT @BranchId, p.id, 0, 0, 0
                FROM catalog.products p
                JOIN catalog.branch_products bp ON bp.product_id = p.id AND bp.branch_id = @BranchId
                WHERE p.id = @ProductId AND p.track_inventory
                ON CONFLICT (branch_id, product_id) DO NOTHING;
                """,
                new { BranchId = branchId, request.ProductId },
                transaction,
                cancellationToken: cancellationToken));

            var balance = await connection.QuerySingleOrDefaultAsync<LockedInventoryBalance>(new CommandDefinition("""
                SELECT
                    product_id AS ProductId,
                    quantity_on_hand AS QuantityOnHand,
                    quantity_reserved AS QuantityReserved
                FROM inventory.branch_inventory
                WHERE branch_id = @BranchId AND product_id = @ProductId
                FOR UPDATE;
                """,
                new { BranchId = branchId, request.ProductId },
                transaction,
                cancellationToken: cancellationToken));
            if (balance is null)
            {
                transaction.Rollback();
                return new(InventoryOperationStatus.ProductNotConfigured);
            }

            var delta = request.MovementType == "ADJUSTMENT_IN" ? request.Quantity : -request.Quantity;
            var after = balance.QuantityOnHand + delta;
            if (after < balance.QuantityReserved)
            {
                transaction.Rollback();
                return new(InventoryOperationStatus.InsufficientStock);
            }

            if (after > 999_999_999.999m)
            {
                transaction.Rollback();
                return new(InventoryOperationStatus.QuantityLimitExceeded);
            }

            var updated = await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE inventory.branch_inventory
                SET quantity_on_hand = @After, updated_at = now(), updated_by = @ActorId
                WHERE branch_id = @BranchId AND product_id = @ProductId;
                """,
                new
                {
                    BranchId = branchId,
                    request.ProductId,
                    After = after,
                    ActorId = actorId
                },
                transaction,
                cancellationToken: cancellationToken));
            if (updated != 1)
            {
                throw new InvalidOperationException("The locked inventory row was not updated while applying the adjustment.");
            }

            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO inventory.stock_movements
                    (branch_id, product_id, movement_type, quantity, quantity_before, quantity_after,
                     reference_type, notes, created_by)
                VALUES
                    (@BranchId, @ProductId, @MovementType, @Delta, @Before, @After,
                     'STOCK_ADJUSTMENT', @Notes, @ActorId);
                """,
                new
                {
                    BranchId = branchId,
                    request.ProductId,
                    request.MovementType,
                    Delta = delta,
                    Before = balance.QuantityOnHand,
                    After = after,
                    request.Notes,
                    ActorId = actorId
                },
                transaction,
                cancellationToken: cancellationToken));
            transaction.Commit();
            return new(InventoryOperationStatus.Succeeded, request.ProductId);
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<InventoryOperationResult> SetReorderLevelAsync(
        Guid branchId,
        Guid productId,
        decimal reorderLevel,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        var affected = await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO inventory.branch_inventory
                (branch_id, product_id, quantity_on_hand, quantity_reserved, reorder_level, updated_by)
            SELECT @BranchId, p.id, 0, 0, @ReorderLevel, @ActorId
            FROM catalog.products p
            JOIN catalog.branch_products bp ON bp.product_id = p.id AND bp.branch_id = @BranchId
            JOIN catalog.product_categories c ON c.id = p.category_id
            WHERE p.id = @ProductId
              AND p.track_inventory
              AND p.is_active
              AND p.deleted_at IS NULL
              AND c.is_active
              AND c.deleted_at IS NULL
            ON CONFLICT (branch_id, product_id) DO UPDATE
            SET reorder_level = EXCLUDED.reorder_level,
                updated_at = now(),
                updated_by = EXCLUDED.updated_by;
            """,
            new { BranchId = branchId, ProductId = productId, ReorderLevel = reorderLevel, ActorId = actorId },
            cancellationToken: cancellationToken));
        return new(affected == 1
            ? InventoryOperationStatus.Succeeded
            : InventoryOperationStatus.ProductNotConfigured,
            productId);
    }

    private static async Task<bool> ReceiptProductsAreConfiguredAsync(
        System.Data.IDbConnection connection,
        IDbTransaction transaction,
        Guid branchId,
        IReadOnlyCollection<GoodsReceiptItemRequest> items,
        CancellationToken cancellationToken) =>
        await ReceiptProductsAreConfiguredAsync(
            connection,
            transaction,
            branchId,
            items.Select(item => item.ProductId).ToArray(),
            cancellationToken);

    private static async Task<bool> ReceiptProductsAreConfiguredAsync(
        System.Data.IDbConnection connection,
        IDbTransaction transaction,
        Guid branchId,
        IReadOnlyCollection<Guid> productIds,
        CancellationToken cancellationToken)
    {
        var count = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT COUNT(DISTINCT p.id)
            FROM catalog.products p
            JOIN catalog.product_categories c ON c.id = p.category_id
            JOIN catalog.branch_products bp ON bp.product_id = p.id AND bp.branch_id = @BranchId
            WHERE p.id = ANY(@ProductIds)
              AND p.track_inventory
              AND p.is_active
              AND p.deleted_at IS NULL
              AND c.is_active
              AND c.deleted_at IS NULL;
            """,
            new { BranchId = branchId, ProductIds = productIds.ToArray() },
            transaction,
            cancellationToken: cancellationToken));
        return count == productIds.Distinct().Count();
    }

    private static async Task InsertReceiptItemsAsync(
        System.Data.IDbConnection connection,
        IDbTransaction transaction,
        Guid receiptId,
        IReadOnlyCollection<GoodsReceiptItemRequest> items,
        CancellationToken cancellationToken)
    {
        foreach (var item in items.OrderBy(item => item.ProductId))
        {
            var lineTotal = decimal.Round(item.Quantity * item.UnitCost, 2, MidpointRounding.AwayFromZero);
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO inventory.goods_receipt_items
                    (goods_receipt_id, product_id, quantity, unit_cost, line_total)
                VALUES
                    (@ReceiptId, @ProductId, @Quantity, @UnitCost, @LineTotal);
                """,
                new
                {
                    ReceiptId = receiptId,
                    item.ProductId,
                    item.Quantity,
                    item.UnitCost,
                    LineTotal = lineTotal
                },
                transaction,
                cancellationToken: cancellationToken));
        }
    }

    private static decimal CalculateTotal(IReadOnlyCollection<GoodsReceiptItemRequest> items) =>
        decimal.Round(
            items.Sum(item => decimal.Round(item.Quantity * item.UnitCost, 2, MidpointRounding.AwayFromZero)),
            2,
            MidpointRounding.AwayFromZero);

    private sealed record ReceiptPostingItem(Guid ProductId, decimal Quantity);

    private sealed record LockedInventoryBalance(
        Guid ProductId,
        decimal QuantityOnHand,
        decimal QuantityReserved);
}
