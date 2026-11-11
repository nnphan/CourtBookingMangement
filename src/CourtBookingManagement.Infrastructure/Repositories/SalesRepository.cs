using System.Data;
using CourtBookingManagement.Application.Abstractions.Data;
using CourtBookingManagement.Application.Matching.Models;
using CourtBookingManagement.Application.Sales.Models;
using CourtBookingManagement.Application.Sales.Models.Requests;
using CourtBookingManagement.Application.Sales.Models.Responses;
using CourtBookingManagement.Application.Sales.Repositories;
using Dapper;

namespace CourtBookingManagement.Infrastructure.Repositories;

public sealed class SalesRepository(ISqlConnectionFactory connectionFactory) : ISalesRepository
{
    public async Task<PagedResult<SalesOrderListItemResponse>> SearchOrdersAsync(
        Guid branchId,
        SalesOrderSearchRequest request,
        CancellationToken cancellationToken)
    {
        const string filterSql = """
            FROM sales.sales_orders so
            LEFT JOIN customer.customers c ON c.id = so.customer_id
            LEFT JOIN sales.invoices i ON i.sales_order_id = so.id
            WHERE so.branch_id = @BranchId
              AND (@Status IS NULL OR so.status = @Status)
              AND (@From::timestamptz IS NULL OR so.created_at >= @From)
              AND (@To::timestamptz IS NULL OR so.created_at <= @To)
              AND (@Search IS NULL OR so.order_code ILIKE @Search
                   OR i.invoice_number ILIKE @Search OR c.full_name ILIKE @Search
                   OR c.phone_number ILIKE @Search)
            """;
        var parameters = new
        {
            BranchId = branchId,
            request.Status,
            request.From,
            request.To,
            Search = string.IsNullOrWhiteSpace(request.Search) ? null : $"%{request.Search}%",
            request.PageSize,
            Offset = (long)(request.Page - 1) * request.PageSize
        };
        const string projection = """
            SELECT
                so.id AS Id,
                so.branch_id AS BranchId,
                so.order_code AS OrderCode,
                c.full_name AS CustomerName,
                so.status AS Status,
                so.total_amount AS TotalAmount,
                so.paid_amount AS PaidAmount,
                so.payment_method_code AS PaymentMethodCode,
                so.payment_status AS PaymentStatus,
                i.id AS InvoiceId,
                i.invoice_number AS InvoiceNumber,
                so.created_at AS CreatedAt
            """;
        using var connection = connectionFactory.CreateConnection();
        var items = (await connection.QueryAsync<SalesOrderListItemResponse>(new CommandDefinition(
            projection + filterSql + " ORDER BY so.created_at DESC, so.id DESC LIMIT @PageSize OFFSET @Offset;",
            parameters,
            cancellationToken: cancellationToken))).AsList();
        var count = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            "SELECT COUNT(*) " + filterSql,
            parameters,
            cancellationToken: cancellationToken));
        return PagedResult<SalesOrderListItemResponse>.Create(items, request.Page, request.PageSize, count);
    }

    public async Task<SalesOrderResponse?> GetOrderAsync(
        Guid branchId,
        Guid orderId,
        CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        var order = await connection.QuerySingleOrDefaultAsync<SalesOrderHeader>(new CommandDefinition("""
            SELECT
                so.id AS Id,
                so.branch_id AS BranchId,
                so.order_code AS OrderCode,
                so.customer_id AS CustomerId,
                c.full_name AS CustomerName,
                so.booking_id AS BookingId,
                so.status AS Status,
                so.subtotal_amount AS SubtotalAmount,
                so.discount_amount AS DiscountAmount,
                so.total_amount AS TotalAmount,
                so.paid_amount AS PaidAmount,
                so.payment_method_code AS PaymentMethodCode,
                so.payment_status AS PaymentStatus,
                so.notes AS Notes,
                so.created_at AS CreatedAt,
                so.completed_at AS CompletedAt,
                i.id AS InvoiceId,
                i.invoice_number AS InvoiceNumber
            FROM sales.sales_orders so
            LEFT JOIN customer.customers c ON c.id = so.customer_id
            LEFT JOIN sales.invoices i ON i.sales_order_id = so.id
            WHERE so.branch_id = @BranchId AND so.id = @OrderId;
            """,
            new { BranchId = branchId, OrderId = orderId },
            cancellationToken: cancellationToken));
        if (order is null)
        {
            return null;
        }

        var items = await connection.QueryAsync<SalesOrderItemResponse>(new CommandDefinition("""
            SELECT
                soi.product_id AS ProductId,
                soi.sku_snapshot AS Sku,
                soi.product_name_snapshot AS ProductName,
                soi.unit_snapshot AS Unit,
                soi.quantity AS Quantity,
                soi.unit_price AS UnitPrice,
                soi.discount_amount AS DiscountAmount,
                soi.line_total AS LineTotal
            FROM sales.sales_order_items soi
            JOIN sales.sales_orders so ON so.id = soi.sales_order_id
            WHERE so.id = @OrderId AND so.branch_id = @BranchId
            ORDER BY soi.id;
            """,
            new { BranchId = branchId, OrderId = orderId },
            cancellationToken: cancellationToken));
        return new SalesOrderResponse(
            order.Id,
            order.BranchId,
            order.OrderCode,
            order.CustomerId,
            order.CustomerName,
            order.BookingId,
            order.Status,
            order.SubtotalAmount,
            order.DiscountAmount,
            order.TotalAmount,
            order.PaidAmount,
            order.PaymentMethodCode,
            order.PaymentStatus,
            order.Notes,
            order.CreatedAt,
            order.CompletedAt,
            order.InvoiceId,
            order.InvoiceNumber,
            items.AsList());
    }

    public async Task<SalesOperationResult> CheckoutAsync(
        Guid branchId,
        CheckoutRequest request,
        Guid cashierId,
        CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);
        try
        {
            var branch = await connection.QuerySingleOrDefaultAsync<BranchSnapshot>(new CommandDefinition("""
                SELECT name AS Name, address AS Address, phone_number AS PhoneNumber
                FROM core.branches
                WHERE id = @BranchId AND is_active AND deleted_at IS NULL;
                """,
                new { BranchId = branchId },
                transaction,
                cancellationToken: cancellationToken));
            if (branch is null)
            {
                transaction.Rollback();
                return new(SalesOperationStatus.RelatedEntityNotFound);
            }

            var paymentMethodId = await connection.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition("""
                SELECT id
                FROM payment.payment_methods
                WHERE code = @Code AND is_active
                LIMIT 1;
                """,
                new { Code = request.PaymentMethodCode },
                transaction,
                cancellationToken: cancellationToken));
            if (paymentMethodId is null)
            {
                transaction.Rollback();
                return new(SalesOperationStatus.RelatedEntityNotFound);
            }

            CustomerSnapshot? customer = null;
            if (request.CustomerId is Guid customerId)
            {
                customer = await connection.QuerySingleOrDefaultAsync<CustomerSnapshot>(new CommandDefinition("""
                    SELECT full_name AS FullName, phone_number AS PhoneNumber
                    FROM customer.customers
                    WHERE id = @CustomerId AND is_active AND deleted_at IS NULL
                    FOR SHARE;
                    """,
                    new { CustomerId = customerId },
                    transaction,
                    cancellationToken: cancellationToken));
                if (customer is null)
                {
                    transaction.Rollback();
                    return new(SalesOperationStatus.RelatedEntityNotFound);
                }
            }

            if (request.BookingId is Guid bookingId)
            {
                var bookingExists = await connection.ExecuteScalarAsync<bool>(new CommandDefinition("""
                    SELECT EXISTS (
                        SELECT 1
                        FROM booking.bookings
                        WHERE id = @BookingId AND branch_id = @BranchId
                          AND is_active AND deleted_at IS NULL
                          AND (@CustomerId::uuid IS NULL OR customer_id = @CustomerId)
                    );
                    """,
                    new { BookingId = bookingId, BranchId = branchId, request.CustomerId },
                    transaction,
                    cancellationToken: cancellationToken));
                if (!bookingExists)
                {
                    transaction.Rollback();
                    return new(SalesOperationStatus.RelatedEntityNotFound);
                }
            }

            var cashierName = await connection.QuerySingleOrDefaultAsync<string>(new CommandDefinition("""
                SELECT full_name
                FROM auth.users
                WHERE id = @CashierId AND is_active AND deleted_at IS NULL;
                """,
                new { CashierId = cashierId },
                transaction,
                cancellationToken: cancellationToken));
            if (string.IsNullOrWhiteSpace(cashierName))
            {
                transaction.Rollback();
                return new(SalesOperationStatus.RelatedEntityNotFound);
            }

            var productIds = request.Items.Select(item => item.ProductId).OrderBy(id => id).ToArray();
            var products = (await connection.QueryAsync<CheckoutProduct>(new CommandDefinition("""
                SELECT
                    p.id AS ProductId,
                    p.sku AS Sku,
                    p.name AS ProductName,
                    p.unit AS Unit,
                    p.track_inventory AS TrackInventory,
                    bp.selling_price AS UnitPrice
                FROM catalog.branch_products bp
                JOIN catalog.products p ON p.id = bp.product_id
                JOIN catalog.product_categories c ON c.id = p.category_id
                WHERE bp.branch_id = @BranchId
                  AND p.id = ANY(@ProductIds)
                  AND bp.is_available_for_sale
                  AND p.is_active
                  AND p.deleted_at IS NULL
                  AND c.is_active
                  AND c.deleted_at IS NULL
                ORDER BY p.id
                FOR UPDATE OF bp, p, c;
                """,
                new { BranchId = branchId, ProductIds = productIds },
                transaction,
                cancellationToken: cancellationToken))).AsList();
            if (products.Count != productIds.Length)
            {
                transaction.Rollback();
                return new(SalesOperationStatus.ProductNotConfigured);
            }

            var quantitiesByProduct = request.Items.ToDictionary(item => item.ProductId, item => item.Quantity);
            var lines = products
                .Select(product =>
                {
                    var quantity = quantitiesByProduct[product.ProductId];
                    var lineTotal = decimal.Round(quantity * product.UnitPrice, 2, MidpointRounding.AwayFromZero);
                    return new CheckoutLine(
                        product.ProductId,
                        product.Sku,
                        product.ProductName,
                        product.Unit,
                        product.TrackInventory,
                        quantity,
                        product.UnitPrice,
                        lineTotal);
                })
                .ToArray();
            var total = lines.Sum(line => line.LineTotal);
            if (total > 9_999_999_999.99m)
            {
                transaction.Rollback();
                return new(SalesOperationStatus.QuantityLimitExceeded);
            }

            var trackedProductIds = lines
                .Where(line => line.TrackInventory)
                .Select(line => line.ProductId)
                .ToArray();
            if (trackedProductIds.Length > 0)
            {
                await connection.ExecuteAsync(new CommandDefinition("""
                    INSERT INTO inventory.branch_inventory
                        (branch_id, product_id, quantity_on_hand, quantity_reserved, reorder_level)
                    SELECT @BranchId, p.id, 0, 0, 0
                    FROM catalog.products p
                    WHERE p.id = ANY(@ProductIds) AND p.track_inventory
                    ON CONFLICT (branch_id, product_id) DO NOTHING;
                    """,
                    new { BranchId = branchId, ProductIds = trackedProductIds },
                    transaction,
                    cancellationToken: cancellationToken));

                await connection.QueryAsync<LockedSaleBalance>(new CommandDefinition("""
                    SELECT
                        product_id AS ProductId,
                        quantity_on_hand AS QuantityOnHand,
                        quantity_reserved AS QuantityReserved
                    FROM inventory.branch_inventory
                    WHERE branch_id = @BranchId AND product_id = ANY(@ProductIds)
                    ORDER BY product_id
                    FOR UPDATE;
                    """,
                    new { BranchId = branchId, ProductIds = trackedProductIds },
                    transaction,
                    cancellationToken: cancellationToken));
            }

            var orderId = Guid.NewGuid();
            var orderCode = $"SO-{DateTimeOffset.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}";
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO sales.sales_orders
                    (id, order_code, branch_id, customer_id, booking_id, status,
                     subtotal_amount, discount_amount, total_amount, paid_amount,
                     payment_method_code, payment_status, notes, completed_at, created_by, updated_by)
                VALUES
                    (@Id, @OrderCode, @BranchId, @CustomerId, @BookingId, 'COMPLETED',
                     @Total, 0, @Total, @Total, @PaymentMethodCode, 'PAID',
                     @Notes, now(), @CashierId, @CashierId);
                """,
                new
                {
                    Id = orderId,
                    OrderCode = orderCode,
                    BranchId = branchId,
                    request.CustomerId,
                    request.BookingId,
                    Total = total,
                    request.PaymentMethodCode,
                    request.Notes,
                    CashierId = cashierId
                },
                transaction,
                cancellationToken: cancellationToken));

            foreach (var line in lines)
            {
                await connection.ExecuteAsync(new CommandDefinition("""
                    INSERT INTO sales.sales_order_items
                        (sales_order_id, product_id, sku_snapshot, product_name_snapshot, unit_snapshot,
                         quantity, unit_price, discount_amount, line_total)
                    VALUES
                        (@OrderId, @ProductId, @Sku, @ProductName, @Unit, @Quantity, @UnitPrice, 0, @LineTotal);
                    """,
                    new
                    {
                        OrderId = orderId,
                        line.ProductId,
                        line.Sku,
                        line.ProductName,
                        line.Unit,
                        line.Quantity,
                        line.UnitPrice,
                        line.LineTotal
                    },
                    transaction,
                    cancellationToken: cancellationToken));

                if (!line.TrackInventory)
                {
                    continue;
                }

                var after = await connection.QuerySingleOrDefaultAsync<decimal?>(new CommandDefinition("""
                    UPDATE inventory.branch_inventory
                    SET quantity_on_hand = quantity_on_hand - @Quantity,
                        updated_at = now(),
                        updated_by = @CashierId
                    WHERE branch_id = @BranchId
                      AND product_id = @ProductId
                      AND quantity_on_hand - @Quantity >= quantity_reserved
                    RETURNING quantity_on_hand;
                    """,
                    new
                    {
                        BranchId = branchId,
                        line.ProductId,
                        line.Quantity,
                        CashierId = cashierId
                    },
                    transaction,
                    cancellationToken: cancellationToken));
                if (after is null)
                {
                    transaction.Rollback();
                    return new(SalesOperationStatus.InsufficientStock);
                }

                var before = after.Value + line.Quantity;
                await connection.ExecuteAsync(new CommandDefinition("""
                    INSERT INTO inventory.stock_movements
                        (branch_id, product_id, movement_type, quantity, quantity_before, quantity_after,
                         reference_type, reference_id, notes, created_by)
                    VALUES
                        (@BranchId, @ProductId, 'SALE', @Quantity, @Before, @After,
                         'SALES_ORDER', @OrderId, 'POS checkout', @CashierId);
                    """,
                    new
                    {
                        BranchId = branchId,
                        line.ProductId,
                        Quantity = -line.Quantity,
                        Before = before,
                        After = after.Value,
                        OrderId = orderId,
                        CashierId = cashierId
                    },
                    transaction,
                    cancellationToken: cancellationToken));
            }

            if (total > 0)
            {
                await connection.ExecuteAsync(new CommandDefinition("""
                    INSERT INTO sales.payments
                        (sales_order_id, payment_method_id, method_code_snapshot, amount, status, paid_at, created_by)
                    VALUES
                        (@OrderId, @PaymentMethodId, @MethodCode, @Amount, 'PAID', now(), @CashierId);
                    """,
                    new
                    {
                        OrderId = orderId,
                        PaymentMethodId = paymentMethodId.Value,
                        MethodCode = request.PaymentMethodCode,
                        Amount = total,
                        CashierId = cashierId
                    },
                    transaction,
                    cancellationToken: cancellationToken));
            }

            var invoiceId = Guid.NewGuid();
            var invoiceNumber = $"INV-{DateTimeOffset.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}";
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO sales.invoices
                    (id, invoice_number, sales_order_id, branch_id, branch_name_snapshot,
                     branch_address_snapshot, branch_phone_snapshot, customer_name_snapshot,
                     customer_phone_snapshot, cashier_name_snapshot, subtotal_amount, discount_amount,
                     total_amount, payment_method_code, payment_status, issued_at)
                VALUES
                    (@InvoiceId, @InvoiceNumber, @OrderId, @BranchId, @BranchName,
                     @BranchAddress, @BranchPhone, @CustomerName, @CustomerPhone, @CashierName,
                     @Total, 0, @Total, @PaymentMethodCode, 'PAID', now());
                """,
                new
                {
                    InvoiceId = invoiceId,
                    InvoiceNumber = invoiceNumber,
                    OrderId = orderId,
                    BranchId = branchId,
                    BranchName = branch.Name,
                    BranchAddress = branch.Address,
                    BranchPhone = branch.PhoneNumber,
                    CustomerName = customer?.FullName,
                    CustomerPhone = customer?.PhoneNumber,
                    CashierName = cashierName,
                    Total = total,
                    request.PaymentMethodCode
                },
                transaction,
                cancellationToken: cancellationToken));
            foreach (var line in lines)
            {
                await connection.ExecuteAsync(new CommandDefinition("""
                    INSERT INTO sales.invoice_items
                        (invoice_id, sku, product_name, unit, quantity, unit_price, line_total)
                    VALUES
                        (@InvoiceId, @Sku, @ProductName, @Unit, @Quantity, @UnitPrice, @LineTotal);
                    """,
                    new
                    {
                        InvoiceId = invoiceId,
                        line.Sku,
                        line.ProductName,
                        line.Unit,
                        line.Quantity,
                        line.UnitPrice,
                        line.LineTotal
                    },
                    transaction,
                    cancellationToken: cancellationToken));
            }

            transaction.Commit();
            return new(SalesOperationStatus.Succeeded, orderId);
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<SalesOperationResult> CancelOrderAsync(
        Guid branchId,
        Guid orderId,
        string reason,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);
        try
        {
            var order = await connection.QuerySingleOrDefaultAsync<CancellableOrder>(new CommandDefinition("""
                SELECT
                    so.status AS Status,
                    so.total_amount AS TotalAmount,
                    p.id AS PaymentId,
                    p.amount AS PaymentAmount,
                    p.status AS PaymentStatus
                FROM sales.sales_orders so
                LEFT JOIN sales.payments p ON p.sales_order_id = so.id
                WHERE so.id = @OrderId AND so.branch_id = @BranchId
                FOR UPDATE OF so;
                """,
                new { OrderId = orderId, BranchId = branchId },
                transaction,
                cancellationToken: cancellationToken));
            if (order is null)
            {
                transaction.Rollback();
                return new(SalesOperationStatus.NotFound);
            }

            if (order.Status != "COMPLETED" ||
                (order.PaymentStatus is not null && order.PaymentStatus != "PAID") ||
                (order.TotalAmount > 0 &&
                 (order.PaymentId is null || order.PaymentAmount != order.TotalAmount)))
            {
                transaction.Rollback();
                return new(SalesOperationStatus.InvalidState);
            }

            var lines = (await connection.QueryAsync<CancellableLine>(new CommandDefinition("""
                SELECT
                    soi.product_id AS ProductId,
                    soi.quantity AS Quantity,
                    EXISTS (
                        SELECT 1
                        FROM inventory.stock_movements sm
                        WHERE sm.branch_id = @BranchId
                          AND sm.product_id = soi.product_id
                          AND sm.movement_type = 'SALE'
                          AND sm.reference_type = 'SALES_ORDER'
                          AND sm.reference_id = @OrderId
                    ) AS TrackInventory
                FROM sales.sales_order_items soi
                WHERE soi.sales_order_id = @OrderId
                ORDER BY soi.product_id;
                """,
                new { OrderId = orderId, BranchId = branchId },
                transaction,
                cancellationToken: cancellationToken))).AsList();
            var trackedIds = lines.Where(line => line.TrackInventory).Select(line => line.ProductId).ToArray();
            if (trackedIds.Length > 0)
            {
                var balances = (await connection.QueryAsync<LockedSaleBalance>(new CommandDefinition("""
                    SELECT
                        product_id AS ProductId,
                        quantity_on_hand AS QuantityOnHand,
                        quantity_reserved AS QuantityReserved
                    FROM inventory.branch_inventory
                    WHERE branch_id = @BranchId AND product_id = ANY(@ProductIds)
                    ORDER BY product_id
                    FOR UPDATE;
                    """,
                    new { BranchId = branchId, ProductIds = trackedIds },
                    transaction,
                    cancellationToken: cancellationToken))).ToDictionary(balance => balance.ProductId);
                if (balances.Count != trackedIds.Length)
                {
                    transaction.Rollback();
                    return new(SalesOperationStatus.InvalidState);
                }

                foreach (var line in lines.Where(line => line.TrackInventory))
                {
                    var before = balances[line.ProductId].QuantityOnHand;
                    var after = before + line.Quantity;
                    if (after > 999_999_999.999m)
                    {
                        transaction.Rollback();
                        return new(SalesOperationStatus.QuantityLimitExceeded);
                    }

                    var inventoryUpdated = await connection.ExecuteAsync(new CommandDefinition("""
                        UPDATE inventory.branch_inventory
                        SET quantity_on_hand = @After, updated_at = now(), updated_by = @ActorId
                        WHERE branch_id = @BranchId AND product_id = @ProductId;
                        """,
                        new
                        {
                            BranchId = branchId,
                            line.ProductId,
                            line.Quantity,
                            Before = before,
                            After = after,
                            OrderId = orderId,
                            ActorId = actorId
                        },
                        transaction,
                        cancellationToken: cancellationToken));
                    if (inventoryUpdated != 1)
                    {
                        throw new InvalidOperationException("The locked inventory row was not updated while cancelling the sale.");
                    }

                    await connection.ExecuteAsync(new CommandDefinition("""
                        INSERT INTO inventory.stock_movements
                            (branch_id, product_id, movement_type, quantity, quantity_before, quantity_after,
                             reference_type, reference_id, notes, created_by)
                        VALUES
                            (@BranchId, @ProductId, 'SALE_RETURN', @Quantity, @Before, @After,
                             'SALES_ORDER', @OrderId, 'Sale cancelled and items returned to stock', @ActorId);
                        """,
                        new
                        {
                            BranchId = branchId,
                            line.ProductId,
                            line.Quantity,
                            Before = before,
                            After = after,
                            OrderId = orderId,
                            ActorId = actorId
                        },
                        transaction,
                        cancellationToken: cancellationToken));
                }
            }

            if (order.PaymentId is Guid paymentId)
            {
                await connection.ExecuteAsync(new CommandDefinition("""
                    INSERT INTO sales.refunds (payment_id, amount, status, reason, processed_at, processed_by)
                    VALUES (@PaymentId, @Amount, 'PROCESSED', @Reason, now(), @ActorId);
                    """,
                    new
                    {
                        PaymentId = paymentId,
                        Amount = order.TotalAmount,
                        Reason = reason,
                        ActorId = actorId
                    },
                    transaction,
                    cancellationToken: cancellationToken));
                var paymentUpdated = await connection.ExecuteAsync(new CommandDefinition("""
                    UPDATE sales.payments
                    SET status = 'REFUNDED'
                    WHERE id = @PaymentId AND status = 'PAID';
                    """,
                    new { PaymentId = paymentId },
                    transaction,
                    cancellationToken: cancellationToken));
                if (paymentUpdated != 1)
                {
                    throw new InvalidOperationException("The locked payment was not refunded while cancelling the sale.");
                }
            }

            var updated = await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE sales.sales_orders
                SET status = 'CANCELLED',
                    payment_status = 'REFUNDED',
                    cancelled_at = now(),
                    cancelled_by = @ActorId,
                    updated_at = now(),
                    updated_by = @ActorId
                WHERE id = @OrderId AND branch_id = @BranchId AND status = 'COMPLETED';
                """,
                new { OrderId = orderId, BranchId = branchId, ActorId = actorId },
                transaction,
                cancellationToken: cancellationToken));
            if (updated != 1)
            {
                throw new InvalidOperationException("The locked sales order was not cancelled.");
            }

            transaction.Commit();
            return new(SalesOperationStatus.Succeeded, orderId);
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<InvoiceSnapshotResponse?> GetInvoiceAsync(
        Guid branchId,
        Guid invoiceId,
        CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        var invoice = await connection.QuerySingleOrDefaultAsync<InvoiceHeader>(new CommandDefinition("""
            SELECT
                id AS Id,
                branch_id AS BranchId,
                sales_order_id AS SalesOrderId,
                invoice_number AS InvoiceNumber,
                branch_name_snapshot AS BranchName,
                branch_address_snapshot AS BranchAddress,
                branch_phone_snapshot AS BranchPhone,
                customer_name_snapshot AS CustomerName,
                customer_phone_snapshot AS CustomerPhone,
                cashier_name_snapshot AS CashierName,
                subtotal_amount AS SubtotalAmount,
                discount_amount AS DiscountAmount,
                total_amount AS TotalAmount,
                payment_method_code AS PaymentMethodCode,
                payment_status AS PaymentStatus,
                issued_at AS IssuedAt
            FROM sales.invoices
            WHERE id = @InvoiceId AND branch_id = @BranchId;
            """,
            new { InvoiceId = invoiceId, BranchId = branchId },
            cancellationToken: cancellationToken));
        if (invoice is null)
        {
            return null;
        }

        var items = await connection.QueryAsync<InvoiceSnapshotItemResponse>(new CommandDefinition("""
            SELECT sku AS Sku, product_name AS ProductName, unit AS Unit,
                   quantity AS Quantity, unit_price AS UnitPrice, line_total AS LineTotal
            FROM sales.invoice_items
            WHERE invoice_id = @InvoiceId
            ORDER BY id;
            """,
            new { InvoiceId = invoiceId },
            cancellationToken: cancellationToken));
        return new InvoiceSnapshotResponse(
            invoice.Id,
            invoice.BranchId,
            invoice.SalesOrderId,
            invoice.InvoiceNumber,
            invoice.BranchName,
            invoice.BranchAddress,
            invoice.BranchPhone,
            invoice.CustomerName,
            invoice.CustomerPhone,
            invoice.CashierName,
            invoice.SubtotalAmount,
            invoice.DiscountAmount,
            invoice.TotalAmount,
            invoice.PaymentMethodCode,
            invoice.PaymentStatus,
            invoice.IssuedAt,
            items.AsList());
    }

    public async Task<SalesReportResponse> GetReportAsync(
        Guid branchId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        var parameters = new
        {
            BranchId = branchId,
            From = from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            ToExclusive = to.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)
        };
        var summary = await connection.QuerySingleAsync<ReportTotals>(new CommandDefinition("""
            SELECT
                COUNT(*) FILTER (WHERE so.status = 'COMPLETED') AS CompletedOrderCount,
                COALESCE(SUM(so.total_amount), 0) AS GrossSales
            FROM sales.sales_orders so
            WHERE so.branch_id = @BranchId
              AND so.created_at >= @From AND so.created_at < @ToExclusive;
            """,
            parameters,
            cancellationToken: cancellationToken));
        var refunds = await connection.ExecuteScalarAsync<decimal>(new CommandDefinition("""
            SELECT COALESCE(SUM(r.amount), 0)
            FROM sales.refunds r
            JOIN sales.payments p ON p.id = r.payment_id
            JOIN sales.sales_orders so ON so.id = p.sales_order_id
            WHERE so.branch_id = @BranchId
              AND r.status = 'PROCESSED'
              AND r.processed_at >= @From AND r.processed_at < @ToExclusive;
            """,
            parameters,
            cancellationToken: cancellationToken));
        var daily = await connection.QueryAsync<DailySalesResponse>(new CommandDefinition("""
            WITH days AS (
                SELECT generate_series(
                    @From::date,
                    (@ToExclusive::date - 1),
                    interval '1 day')::date AS report_date
            ),
            daily_orders AS (
                SELECT
                    (so.created_at AT TIME ZONE 'UTC')::date AS report_date,
                    COUNT(*) FILTER (WHERE so.status = 'COMPLETED') AS order_count,
                    COALESCE(SUM(so.total_amount), 0) AS gross_sales
                FROM sales.sales_orders so
                WHERE so.branch_id = @BranchId
                  AND so.created_at >= @From AND so.created_at < @ToExclusive
                GROUP BY (so.created_at AT TIME ZONE 'UTC')::date
            ),
            daily_refunds AS (
                SELECT (r.processed_at AT TIME ZONE 'UTC')::date AS report_date, SUM(r.amount) AS refunds
                FROM sales.refunds r
                JOIN sales.payments p ON p.id = r.payment_id
                JOIN sales.sales_orders so ON so.id = p.sales_order_id
                WHERE so.branch_id = @BranchId
                  AND r.status = 'PROCESSED'
                  AND r.processed_at >= @From AND r.processed_at < @ToExclusive
                GROUP BY (r.processed_at AT TIME ZONE 'UTC')::date
            )
            SELECT
                days.report_date AS Date,
                COALESCE(daily_orders.order_count, 0) AS OrderCount,
                COALESCE(daily_orders.gross_sales, 0) AS GrossSales,
                COALESCE(daily_refunds.refunds, 0) AS Refunds
            FROM days
            LEFT JOIN daily_orders ON daily_orders.report_date = days.report_date
            LEFT JOIN daily_refunds ON daily_refunds.report_date = days.report_date
            ORDER BY days.report_date;
            """,
            parameters,
            cancellationToken: cancellationToken));
        var topProducts = await connection.QueryAsync<TopSellingProductResponse>(new CommandDefinition("""
            SELECT
                soi.product_id AS ProductId,
                soi.sku_snapshot AS Sku,
                soi.product_name_snapshot AS ProductName,
                SUM(soi.quantity) AS QuantitySold,
                SUM(soi.line_total) AS GrossSales
            FROM sales.sales_order_items soi
            JOIN sales.sales_orders so ON so.id = soi.sales_order_id
            WHERE so.branch_id = @BranchId
              AND so.created_at >= @From AND so.created_at < @ToExclusive
            GROUP BY soi.product_id, soi.sku_snapshot, soi.product_name_snapshot
            ORDER BY SUM(soi.line_total) DESC, soi.product_name_snapshot
            LIMIT 10;
            """,
            parameters,
            cancellationToken: cancellationToken));
        return new SalesReportResponse(
            from,
            to,
            summary.CompletedOrderCount,
            summary.GrossSales,
            refunds,
            summary.GrossSales - refunds,
            daily.AsList(),
            topProducts.AsList());
    }

    private sealed record CheckoutProduct(
        Guid ProductId,
        string Sku,
        string ProductName,
        string Unit,
        bool TrackInventory,
        decimal UnitPrice);

    private sealed record CheckoutLine(
        Guid ProductId,
        string Sku,
        string ProductName,
        string Unit,
        bool TrackInventory,
        decimal Quantity,
        decimal UnitPrice,
        decimal LineTotal);

    private sealed record LockedSaleBalance(
        Guid ProductId,
        decimal QuantityOnHand,
        decimal QuantityReserved);

    private sealed record BranchSnapshot(string Name, string Address, string? PhoneNumber);

    private sealed record CustomerSnapshot(string FullName, string PhoneNumber);

    private sealed record CancellableOrder(
        string Status,
        decimal TotalAmount,
        Guid? PaymentId,
        decimal? PaymentAmount,
        string? PaymentStatus);

    private sealed record CancellableLine(Guid ProductId, decimal Quantity, bool TrackInventory);

    private sealed record SalesOrderHeader(
        Guid Id,
        Guid BranchId,
        string OrderCode,
        Guid? CustomerId,
        string? CustomerName,
        Guid? BookingId,
        string Status,
        decimal SubtotalAmount,
        decimal DiscountAmount,
        decimal TotalAmount,
        decimal PaidAmount,
        string PaymentMethodCode,
        string PaymentStatus,
        string? Notes,
        DateTimeOffset CreatedAt,
        DateTimeOffset? CompletedAt,
        Guid? InvoiceId,
        string? InvoiceNumber);

    private sealed record InvoiceHeader(
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
        DateTimeOffset IssuedAt);

    private sealed record ReportTotals(long CompletedOrderCount, decimal GrossSales);
}
