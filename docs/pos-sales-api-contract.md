# POS and Sales API Contract

This document records the schema and contract decisions for the branch-scoped POS feature. Backend DTOs and Swagger are authoritative; this document and frontend contracts must be updated together when endpoints are implemented.

## Confirmed domain decisions

- Products and categories are shared catalog records. Branch enablement, selling/cost prices, and inventory are Branch-specific.
- Each size/color combination is a separate SKU.
- Order statuses for the initial checkout flow: `COMPLETED`, `CANCELLED`. The schema permits later workflow statuses.
- Goods receipt statuses: `DRAFT`, `POSTED`, `CANCELLED`.
- MVP payment methods use existing codes: `CASH`, `MOMO`, `BANK_TRANSFER`.
- Retail payment records are stored in `sales.payments`; the existing `payment.payments` relation is booking-specific and is not modified.
- The selected PDF library is PdfSharpCore under its MIT license; A4 is the MVP template. Vietnamese text requires a bundled Unicode font and PDF font embedding. PDFsharp 6.x was not selected because its current license is AGPL/commercial.
- Global `ADMIN` access remains global. Branch roles are assigned through `auth.branch_user_roles`; an existing `BRANCH_OWNER` may act as `BRANCH_ADMIN` only for branches where `core.branches.owner_id` maps through `core.owners.user_id`. `STAFF` requires an explicit active branch assignment.
- Completed-order cancellation is an audited, manual/offline full-refund flow; no payment-provider refund integration is implied.
- Money uses `numeric(12,2)` consistent with the existing payment/booking entities. Inventory quantities use `numeric(12,3)`.

## Schema and lifecycle

The migration `Database/Migrations/20261111_AddBranchScopedPosSales.sql` creates:

- `catalog.product_categories`, `catalog.products`, `catalog.branch_products`
- `inventory.branch_inventory`, `inventory.stock_movements`, `inventory.goods_receipts`, `inventory.goods_receipt_items`
- `sales.sales_orders`, `sales.sales_order_items`, `sales.payments`, `sales.refunds`, `sales.invoices`, `sales.invoice_items`
- `auth.branch_user_roles`, new POS permission rows, and role permission assignments

Sales items and invoice items preserve product snapshots. Invoice header snapshots preserve branch, customer, cashier, payment, and amount data. Posting receipts and checkout must write stock movements within the same transaction as the quantity changes. Completed receipts, orders, and invoices are not hard-deleted.

## API contract status

Catalog, inventory, and branch-scoped sales endpoints are implemented in the API. The frontend provides branch-scoped POS checkout, product/category setup, inventory and receipt management, order history, reports, and invoice preview/download from the `/pos-sales` route.

The existing `GET /api/branches/my-branches` lookup now returns active branches owned by the user or assigned through an active `BRANCH_ADMIN`/`STAFF` branch role; global administrators can select any active branch. This lets the shared branch selector serve the branch-scoped commerce pages without exposing unrelated branches to branch users.

### Product categories

Base route: `/api/product-categories`

| Method | Route | Permission | Result |
|---|---|---|---|
| GET | `/` | `product.view` | Category list; optional `search` and `isActive` filters |
| GET | `/{id}` | `product.view` | One category |
| POST | `/` | `product.create` | Create category; returns `201` and the category |
| PUT | `/{id}` | `product.update` | Update category |
| DELETE | `/{id}` | `product.deactivate` | Soft-deactivate category |

Create/update bodies accept `code`, `name`, optional `description`, `displayOrder`, and `isActive`. Codes are trimmed and uppercased by the service.

### Products

Base route: `/api/products`

| Method | Route | Permission | Result |
|---|---|---|---|
| GET | `/` | `product.view` | Filtered, paged product list |
| GET | `/{id}` | `product.view` | One product |
| POST | `/` | `product.create` | Create product; returns `201` and the product |
| PUT | `/{id}` | `product.update` | Update product |
| DELETE | `/{id}` | `product.deactivate` | Soft-deactivate product |

List filters: `search`, `categoryId`, `isActive`, and `trackInventory`. Paging uses `page` (default `1`) and `pageSize` (default `20`, maximum `100`). `sort` accepts `name_asc`, `name_desc`, `sku_asc`, `price_asc`, or `price_desc`. The success envelope contains the items in `data` and page information in `metadata`.

Create/update bodies accept `categoryId`, `sku`, optional `barcode`, `name`, optional `description`, `unit`, optional `defaultCostPrice`, `defaultSellingPrice`, `trackInventory`, and `isActive`.

### Branch product configuration

Base route: `/api/branches/{branchId}/products`

| Method | Route | Permission | Result |
|---|---|---|---|
| GET | `/` | `product.view` | Configured branch products; optional `isAvailableForSale` filter |
| GET | `/{productId}` | `product.view` | One configured product |
| PUT | `/{productId}` | `product.update` | Upsert one branch product configuration |
| POST | `/bulk-configure` | `product.update` | Atomically upsert configurations for the submitted products |

Branch routes additionally enforce access to the specified branch in the application service. Configuration bodies accept `sellingPrice`, optional `costPrice`, `isAvailableForSale`, and `displayOrder`; bulk requests wrap a list of those fields plus `productId` in `items`. Enabling an inactive product or a product in an inactive category is rejected. Inventory tracking products receive a zero-quantity inventory row when first configured.

### Response and error shape

Successful requests use the existing response envelope:

```json
{
  "success": true,
  "message": "Products retrieved successfully.",
  "data": [],
  "metadata": {
    "pageNumber": 1,
    "pageSize": 20,
    "totalCount": 0,
    "totalPages": 0,
    "hasPreviousPage": false,
    "hasNextPage": false
  },
  "traceId": "...",
  "timestamp": "..."
}
```

Errors use the existing `ApiErrorResponse`. Catalog error codes include `Catalog.CategoryNotFound`, `Catalog.ProductNotFound`, `Catalog.BranchNotFound`, `Catalog.CategoryCodeExists`, `Catalog.ProductSkuExists`, `Catalog.ProductBarcodeExists`, `Catalog.ProductInactive`, `Catalog.CategoryInactive`, `Catalog.InvalidPagination`, and `Catalog.InvalidSort`. Not-found errors map to `404`, duplicate/inactive conflicts to `409`, invalid filters to `400`, and branch authorization failures to `403`.

### Branch inventory and stock movements

Base route: `/api/branches/{branchId}/inventory`

| Method | Route | Permission | Result |
|---|---|---|---|
| GET | `/` | `inventory.view` | Paged branch inventory balances; accepts `search`, `lowStockOnly`, `page`, and `pageSize` |
| GET | `/movements` | `inventory.view` | Paged stock movement ledger; accepts `productId`, `movementType`, `from`, `to`, `page`, and `pageSize` |
| POST | `/adjustments` | `inventory.adjust` | Atomic manual stock increase/decrease with movement audit |
| PUT | `/{productId}/reorder-level` | `inventory.adjust` | Set the branch-specific low-stock threshold |

Inventory balance responses include on-hand, reserved, available, and reorder-level quantities. Inventory results are limited to active, configured, inventory-tracked products in the authorized branch. Adjustment bodies accept `productId`, positive `quantity`, `movementType` (`ADJUSTMENT_IN`, `ADJUSTMENT_OUT`, or `DAMAGE`), and required `notes`. Outbound adjustments and damage reduce stock and fail without mutation if they would make stock negative. Every successful adjustment records the signed quantity and before/after balances in `inventory.stock_movements`.
The reorder-level body contains a non-negative `reorderLevel` quantity with up to three decimal places; changing the threshold does not alter on-hand stock or write a stock movement.

### Goods receipts

Goods receipt routes use the same branch-scoped inventory base:

| Method | Route | Permission | Result |
|---|---|---|---|
| GET | `/goods-receipts` | `inventory.view` | Paged receipts; optional `status` and `search` filters |
| GET | `/goods-receipts/{receiptId}` | `inventory.view` | Receipt detail including lines |
| POST | `/goods-receipts` | `inventory.receive` | Create a `DRAFT` receipt |
| PUT | `/goods-receipts/{receiptId}` | `inventory.receive` | Replace draft header fields and lines |
| POST | `/goods-receipts/{receiptId}/post` | `inventory.post_receipt` | Atomically post receipt, increase stock, and record movements |
| POST | `/goods-receipts/{receiptId}/cancel` | `inventory.receive` | Cancel a draft without changing stock |

Draft bodies contain optional `supplierName`, `supplierReference`, `notes`, and `items` (`productId`, positive `quantity`, non-negative `unitCost`). Item quantities allow at most three decimal places and unit costs at most two. The service calculates line and receipt totals; clients do not submit totals. Only active inventory-tracked products configured for the branch may be received. Posting locks the draft and inventory rows in stable product-ID order, applies each stock increase, writes a `GOODS_RECEIPT` movement, and changes the receipt to `POSTED` in one database transaction. Drafts can be edited or cancelled; posted receipts cannot be edited or cancelled, preserving the stock audit trail.

Inventory error codes include `Inventory.ReceiptNotFound`, `Inventory.ReceiptNotDraft`, `Inventory.ProductNotConfigured`, `Inventory.InsufficientStock`, `Inventory.QuantityLimitExceeded`, `Inventory.InvalidRequest`, `Inventory.InvalidPagination`, and `Inventory.InvalidFilter`. All inventory and receipt queries and mutations include the route's `branchId` in repository predicates, and each service operation checks branch-level permission before accessing data.

### POS sales, reports, and invoices

Base route: `/api/branches/{branchId}/sales`

| Method | Route | Permission | Result |
|---|---|---|---|
| GET | `/orders` | `sales.view` | Paged branch order list; accepts `search`, `status`, `from`, `to`, `page`, and `pageSize` |
| POST | `/orders` | `sales.create` | Complete checkout and return the order with `201 Created` and a location for the order |
| GET | `/orders/{orderId}` | `sales.view` | Order detail with immutable item snapshots |
| POST | `/orders/{orderId}/cancel` | `sales.cancel` + `sales.refund` | Cancel a completed order, return tracked stock, and record a manual full refund |
| GET | `/report` | `sales.report.view` | Gross/refund/net totals, daily totals, and the top 10 checkout products for an inclusive date range |
| GET | `/invoices/{invoiceId}` | `sales.invoice.view` | Immutable invoice header and item snapshot |
| GET | `/invoices/{invoiceId}/preview` | `sales.invoice.view` | Server-generated PDF with `Content-Type: application/pdf` and inline `Content-Disposition` |
| GET | `/invoices/{invoiceId}/download` | `sales.invoice.print` | Server-generated PDF with attachment `Content-Disposition` for download/printing |

Checkout accepts `customerId` and `bookingId` as optional UUIDs, `paymentMethodCode` (`CASH`, `MOMO`, or `BANK_TRANSFER`), optional `notes`, and `items` containing only `productId` and positive `quantity`. A booking reference must belong to the same branch and, when a customer is also supplied, match that customer. Prices and totals are read/calculated server-side from the branch product configuration. The cashier records the selected payment method as paid; no external payment-provider capture is performed by this MVP.

Checkout locks configured product and tracked inventory rows in product-ID order, checks available stock, then writes the order, item snapshots, payment, invoice snapshots, stock decrements, and `SALE` movements in one transaction. Any insufficient-stock or write failure rolls the transaction back. A cancellation is allowed only for a completed order, requires both cancellation and refund permissions, is serialized by locking the order row, restores tracked quantities, writes `SALE_RETURN` movements, and records a full processed refund for positive-value orders in one transaction. Zero-value orders have no payment/refund row. Repeated cancellation is rejected.

Reports accept inclusive ISO `from` and `to` dates with a maximum span of 366 days. Date boundaries and daily grouping use UTC. `GrossSales` includes checkouts created in the range, including orders later cancelled; processed refunds are grouped by their processing date and `NetSales` is gross sales minus those refunds. `CompletedOrderCount` counts orders currently in `COMPLETED` state. Top products represent checkout volume before returns. This keeps sale and refund dates independently reportable.

Invoice snapshots capture branch, customer, cashier, payment, amount, and item details at checkout. Preview, download, and reprint render PDFs from those saved snapshots; they do not read mutable product or branch labels. PDF responses are not wrapped in the JSON API envelope. The preview response uses `inline; filename="invoice-{invoiceId}.pdf"` and download uses an attachment filename. PDFs use the bundled Noto Sans font (SIL Open Font License) for Vietnamese text and format VND amounts.

Sales error codes include `Sales.OrderNotFound`, `Sales.InvoiceNotFound`, `Sales.OrderNotCompletable`, `Sales.ProductNotAvailable`, `Sales.InsufficientStock`, `Sales.QuantityLimitExceeded`, `Sales.RelatedEntityNotFound`, `Sales.InvalidRequest`, `Sales.InvalidPagination`, and `Sales.InvalidFilter`. Not-found errors map to `404`, state/product/stock conflicts to `409`, invalid filters or pagination to `400`, and branch authorization failures to `403`. All order, invoice, and report queries are constrained to the route's `branchId`; services also check the corresponding branch permission.

## Migration execution

The repository currently uses forward-only, handwritten SQL files wrapped in transactions and has no migration runner or down-migration convention. Apply migrations using the repository's existing database deployment process; do not execute this schema automatically at application startup.
