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

Catalog endpoints are implemented in the API. Inventory, Sales, and frontend endpoints remain for their later phases.

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

## Migration execution

The repository currently uses forward-only, handwritten SQL files wrapped in transactions and has no migration runner or down-migration convention. Apply migrations using the repository's existing database deployment process; do not execute this schema automatically at application startup.
