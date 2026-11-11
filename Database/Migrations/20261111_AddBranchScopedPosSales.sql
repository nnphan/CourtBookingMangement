BEGIN;

CREATE SCHEMA IF NOT EXISTS catalog;
CREATE SCHEMA IF NOT EXISTS inventory;
CREATE SCHEMA IF NOT EXISTS sales;

CREATE TABLE catalog.product_categories (
    id uuid PRIMARY KEY DEFAULT uuid_generate_v7(),
    code varchar(50) NOT NULL UNIQUE,
    name varchar(150) NOT NULL,
    description text NULL,
    display_order integer NOT NULL DEFAULT 0,
    is_active boolean NOT NULL DEFAULT true,
    created_at timestamptz NOT NULL DEFAULT now(),
    created_by uuid NULL REFERENCES auth.users(id),
    updated_at timestamptz NOT NULL DEFAULT now(),
    updated_by uuid NULL REFERENCES auth.users(id),
    deleted_at timestamptz NULL,
    deleted_by uuid NULL REFERENCES auth.users(id),
    CONSTRAINT ck_product_categories_code_not_blank CHECK (length(trim(code)) > 0),
    CONSTRAINT ck_product_categories_name_not_blank CHECK (length(trim(name)) > 0)
);

CREATE TABLE catalog.products (
    id uuid PRIMARY KEY DEFAULT uuid_generate_v7(),
    category_id uuid NOT NULL REFERENCES catalog.product_categories(id) ON DELETE RESTRICT,
    sku varchar(100) NOT NULL UNIQUE,
    barcode varchar(100) NULL,
    name varchar(200) NOT NULL,
    description text NULL,
    unit varchar(50) NOT NULL,
    default_cost_price numeric(12, 2) NULL,
    default_selling_price numeric(12, 2) NOT NULL,
    track_inventory boolean NOT NULL DEFAULT true,
    is_active boolean NOT NULL DEFAULT true,
    created_at timestamptz NOT NULL DEFAULT now(),
    created_by uuid NULL REFERENCES auth.users(id),
    updated_at timestamptz NOT NULL DEFAULT now(),
    updated_by uuid NULL REFERENCES auth.users(id),
    deleted_at timestamptz NULL,
    deleted_by uuid NULL REFERENCES auth.users(id),
    CONSTRAINT ck_products_sku_not_blank CHECK (length(trim(sku)) > 0),
    CONSTRAINT ck_products_name_not_blank CHECK (length(trim(name)) > 0),
    CONSTRAINT ck_products_unit_not_blank CHECK (length(trim(unit)) > 0),
    CONSTRAINT ck_products_default_cost_nonnegative CHECK (default_cost_price IS NULL OR default_cost_price >= 0),
    CONSTRAINT ck_products_default_selling_nonnegative CHECK (default_selling_price >= 0)
);

CREATE UNIQUE INDEX ux_products_barcode
    ON catalog.products (barcode)
    WHERE barcode IS NOT NULL;
CREATE INDEX ix_products_category_id ON catalog.products (category_id);

CREATE TABLE catalog.branch_products (
    id uuid PRIMARY KEY DEFAULT uuid_generate_v7(),
    branch_id uuid NOT NULL REFERENCES core.branches(id) ON DELETE RESTRICT,
    product_id uuid NOT NULL REFERENCES catalog.products(id) ON DELETE RESTRICT,
    selling_price numeric(12, 2) NOT NULL,
    cost_price numeric(12, 2) NULL,
    is_available_for_sale boolean NOT NULL DEFAULT true,
    display_order integer NOT NULL DEFAULT 0,
    created_at timestamptz NOT NULL DEFAULT now(),
    created_by uuid NULL REFERENCES auth.users(id),
    updated_at timestamptz NOT NULL DEFAULT now(),
    updated_by uuid NULL REFERENCES auth.users(id),
    CONSTRAINT ux_branch_products_branch_product UNIQUE (branch_id, product_id),
    CONSTRAINT ck_branch_products_selling_nonnegative CHECK (selling_price >= 0),
    CONSTRAINT ck_branch_products_cost_nonnegative CHECK (cost_price IS NULL OR cost_price >= 0)
);
CREATE INDEX ix_branch_products_available ON catalog.branch_products (branch_id, is_available_for_sale);

CREATE TABLE inventory.branch_inventory (
    id uuid PRIMARY KEY DEFAULT uuid_generate_v7(),
    branch_id uuid NOT NULL REFERENCES core.branches(id) ON DELETE RESTRICT,
    product_id uuid NOT NULL REFERENCES catalog.products(id) ON DELETE RESTRICT,
    quantity_on_hand numeric(12, 3) NOT NULL DEFAULT 0,
    quantity_reserved numeric(12, 3) NOT NULL DEFAULT 0,
    reorder_level numeric(12, 3) NOT NULL DEFAULT 0,
    updated_at timestamptz NOT NULL DEFAULT now(),
    updated_by uuid NULL REFERENCES auth.users(id),
    CONSTRAINT ux_branch_inventory_branch_product UNIQUE (branch_id, product_id),
    CONSTRAINT ck_branch_inventory_on_hand_nonnegative CHECK (quantity_on_hand >= 0),
    CONSTRAINT ck_branch_inventory_reserved_nonnegative CHECK (quantity_reserved >= 0),
    CONSTRAINT ck_branch_inventory_reserved_not_over_on_hand CHECK (quantity_reserved <= quantity_on_hand),
    CONSTRAINT ck_branch_inventory_reorder_nonnegative CHECK (reorder_level >= 0)
);

CREATE TABLE inventory.stock_movements (
    id uuid PRIMARY KEY DEFAULT uuid_generate_v7(),
    branch_id uuid NOT NULL REFERENCES core.branches(id) ON DELETE RESTRICT,
    product_id uuid NOT NULL REFERENCES catalog.products(id) ON DELETE RESTRICT,
    movement_type varchar(30) NOT NULL,
    quantity numeric(12, 3) NOT NULL,
    quantity_before numeric(12, 3) NOT NULL,
    quantity_after numeric(12, 3) NOT NULL,
    reference_type varchar(50) NULL,
    reference_id uuid NULL,
    notes text NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    created_by uuid NULL REFERENCES auth.users(id),
    CONSTRAINT ck_stock_movements_type CHECK (movement_type IN (
        'OPENING_BALANCE', 'GOODS_RECEIPT', 'SALE', 'SALE_RETURN',
        'ADJUSTMENT_IN', 'ADJUSTMENT_OUT', 'DAMAGE', 'TRANSFER_IN', 'TRANSFER_OUT'
    )),
    CONSTRAINT ck_stock_movements_quantity_nonzero CHECK (quantity <> 0),
    CONSTRAINT ck_stock_movements_before_nonnegative CHECK (quantity_before >= 0),
    CONSTRAINT ck_stock_movements_after_nonnegative CHECK (quantity_after >= 0),
    CONSTRAINT ck_stock_movements_quantity_matches_balance CHECK (quantity_after = quantity_before + quantity)
);
CREATE INDEX ix_stock_movements_branch_created ON inventory.stock_movements (branch_id, created_at DESC);
CREATE INDEX ix_stock_movements_product_created ON inventory.stock_movements (product_id, created_at DESC);

CREATE TABLE inventory.goods_receipts (
    id uuid PRIMARY KEY DEFAULT uuid_generate_v7(),
    receipt_code varchar(50) NOT NULL UNIQUE,
    branch_id uuid NOT NULL REFERENCES core.branches(id) ON DELETE RESTRICT,
    supplier_name varchar(200) NULL,
    supplier_reference varchar(100) NULL,
    status varchar(20) NOT NULL DEFAULT 'DRAFT',
    total_cost numeric(12, 2) NOT NULL DEFAULT 0,
    notes text NULL,
    received_at timestamptz NULL,
    received_by uuid NULL REFERENCES auth.users(id),
    posted_at timestamptz NULL,
    posted_by uuid NULL REFERENCES auth.users(id),
    cancelled_at timestamptz NULL,
    cancelled_by uuid NULL REFERENCES auth.users(id),
    created_at timestamptz NOT NULL DEFAULT now(),
    created_by uuid NULL REFERENCES auth.users(id),
    updated_at timestamptz NOT NULL DEFAULT now(),
    updated_by uuid NULL REFERENCES auth.users(id),
    CONSTRAINT ck_goods_receipts_status CHECK (status IN ('DRAFT', 'POSTED', 'CANCELLED')),
    CONSTRAINT ck_goods_receipts_total_nonnegative CHECK (total_cost >= 0)
);
CREATE INDEX ix_goods_receipts_branch_created ON inventory.goods_receipts (branch_id, created_at DESC);

CREATE TABLE inventory.goods_receipt_items (
    id uuid PRIMARY KEY DEFAULT uuid_generate_v7(),
    goods_receipt_id uuid NOT NULL REFERENCES inventory.goods_receipts(id) ON DELETE RESTRICT,
    product_id uuid NOT NULL REFERENCES catalog.products(id) ON DELETE RESTRICT,
    quantity numeric(12, 3) NOT NULL,
    unit_cost numeric(12, 2) NOT NULL,
    line_total numeric(12, 2) NOT NULL,
    CONSTRAINT ux_goods_receipt_items_receipt_product UNIQUE (goods_receipt_id, product_id),
    CONSTRAINT ck_goods_receipt_items_quantity_positive CHECK (quantity > 0),
    CONSTRAINT ck_goods_receipt_items_unit_cost_nonnegative CHECK (unit_cost >= 0),
    CONSTRAINT ck_goods_receipt_items_line_total_nonnegative CHECK (line_total >= 0)
);

CREATE TABLE sales.sales_orders (
    id uuid PRIMARY KEY DEFAULT uuid_generate_v7(),
    order_code varchar(50) NOT NULL UNIQUE,
    branch_id uuid NOT NULL REFERENCES core.branches(id) ON DELETE RESTRICT,
    customer_id uuid NULL REFERENCES customer.customers(id) ON DELETE RESTRICT,
    booking_id uuid NULL REFERENCES booking.bookings(id) ON DELETE RESTRICT,
    status varchar(30) NOT NULL,
    subtotal_amount numeric(12, 2) NOT NULL,
    discount_amount numeric(12, 2) NOT NULL DEFAULT 0,
    total_amount numeric(12, 2) NOT NULL,
    paid_amount numeric(12, 2) NOT NULL DEFAULT 0,
    payment_method_code varchar(30) NOT NULL,
    payment_status varchar(30) NOT NULL,
    notes text NULL,
    completed_at timestamptz NULL,
    cancelled_at timestamptz NULL,
    cancelled_by uuid NULL REFERENCES auth.users(id),
    created_at timestamptz NOT NULL DEFAULT now(),
    created_by uuid NULL REFERENCES auth.users(id),
    updated_at timestamptz NOT NULL DEFAULT now(),
    updated_by uuid NULL REFERENCES auth.users(id),
    CONSTRAINT ck_sales_orders_status CHECK (status IN (
        'DRAFT', 'PENDING_PAYMENT', 'COMPLETED', 'CANCELLED', 'PARTIALLY_REFUNDED', 'REFUNDED'
    )),
    CONSTRAINT ck_sales_orders_payment_status CHECK (payment_status IN (
        'UNPAID', 'PARTIALLY_PAID', 'PAID', 'PARTIALLY_REFUNDED', 'REFUNDED'
    )),
    CONSTRAINT ck_sales_orders_subtotal_nonnegative CHECK (subtotal_amount >= 0),
    CONSTRAINT ck_sales_orders_discount_valid CHECK (discount_amount >= 0 AND discount_amount <= subtotal_amount),
    CONSTRAINT ck_sales_orders_total_nonnegative CHECK (total_amount >= 0),
    CONSTRAINT ck_sales_orders_paid_nonnegative CHECK (paid_amount >= 0)
);
CREATE INDEX ix_sales_orders_branch_created ON sales.sales_orders (branch_id, created_at DESC);
CREATE INDEX ix_sales_orders_booking_id ON sales.sales_orders (booking_id) WHERE booking_id IS NOT NULL;

CREATE TABLE sales.sales_order_items (
    id uuid PRIMARY KEY DEFAULT uuid_generate_v7(),
    sales_order_id uuid NOT NULL REFERENCES sales.sales_orders(id) ON DELETE RESTRICT,
    product_id uuid NOT NULL REFERENCES catalog.products(id) ON DELETE RESTRICT,
    sku_snapshot varchar(100) NOT NULL,
    product_name_snapshot varchar(200) NOT NULL,
    unit_snapshot varchar(50) NOT NULL,
    quantity numeric(12, 3) NOT NULL,
    unit_price numeric(12, 2) NOT NULL,
    discount_amount numeric(12, 2) NOT NULL DEFAULT 0,
    line_total numeric(12, 2) NOT NULL,
    CONSTRAINT ck_sales_order_items_quantity_positive CHECK (quantity > 0),
    CONSTRAINT ck_sales_order_items_unit_price_nonnegative CHECK (unit_price >= 0),
    CONSTRAINT ck_sales_order_items_discount_nonnegative CHECK (discount_amount >= 0),
    CONSTRAINT ck_sales_order_items_line_total_nonnegative CHECK (line_total >= 0)
);
CREATE INDEX ix_sales_order_items_order_id ON sales.sales_order_items (sales_order_id);

CREATE TABLE sales.payments (
    id uuid PRIMARY KEY DEFAULT uuid_generate_v7(),
    sales_order_id uuid NOT NULL REFERENCES sales.sales_orders(id) ON DELETE RESTRICT,
    payment_method_id uuid NOT NULL REFERENCES payment.payment_methods(id) ON DELETE RESTRICT,
    method_code_snapshot varchar(30) NOT NULL,
    amount numeric(12, 2) NOT NULL,
    status varchar(20) NOT NULL,
    paid_at timestamptz NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    created_by uuid NULL REFERENCES auth.users(id),
    CONSTRAINT ux_sales_payments_order UNIQUE (sales_order_id),
    CONSTRAINT ck_sales_payments_amount_positive CHECK (amount > 0),
    CONSTRAINT ck_sales_payments_status CHECK (status IN ('PENDING', 'PAID', 'REFUNDED'))
);

CREATE TABLE sales.refunds (
    id uuid PRIMARY KEY DEFAULT uuid_generate_v7(),
    payment_id uuid NOT NULL REFERENCES sales.payments(id) ON DELETE RESTRICT,
    amount numeric(12, 2) NOT NULL,
    status varchar(20) NOT NULL,
    reason text NOT NULL,
    requested_at timestamptz NOT NULL DEFAULT now(),
    processed_at timestamptz NULL,
    processed_by uuid NULL REFERENCES auth.users(id),
    CONSTRAINT ck_sales_refunds_amount_positive CHECK (amount > 0),
    CONSTRAINT ck_sales_refunds_status CHECK (status IN ('PENDING', 'PROCESSED', 'FAILED'))
);
CREATE INDEX ix_sales_refunds_payment_id ON sales.refunds (payment_id);

CREATE TABLE sales.invoices (
    id uuid PRIMARY KEY DEFAULT uuid_generate_v7(),
    invoice_number varchar(50) NOT NULL UNIQUE,
    sales_order_id uuid NOT NULL UNIQUE REFERENCES sales.sales_orders(id) ON DELETE RESTRICT,
    branch_id uuid NOT NULL REFERENCES core.branches(id) ON DELETE RESTRICT,
    branch_name_snapshot varchar(150) NOT NULL,
    branch_address_snapshot varchar(255) NULL,
    branch_phone_snapshot varchar(20) NULL,
    customer_name_snapshot varchar(150) NULL,
    customer_phone_snapshot varchar(20) NULL,
    cashier_name_snapshot varchar(255) NOT NULL,
    subtotal_amount numeric(12, 2) NOT NULL,
    discount_amount numeric(12, 2) NOT NULL,
    total_amount numeric(12, 2) NOT NULL,
    payment_method_code varchar(30) NOT NULL,
    payment_status varchar(30) NOT NULL,
    issued_at timestamptz NOT NULL,
    CONSTRAINT ck_sales_invoices_subtotal_nonnegative CHECK (subtotal_amount >= 0),
    CONSTRAINT ck_sales_invoices_discount_valid CHECK (discount_amount >= 0 AND discount_amount <= subtotal_amount),
    CONSTRAINT ck_sales_invoices_total_nonnegative CHECK (total_amount >= 0)
);
CREATE INDEX ix_sales_invoices_branch_issued ON sales.invoices (branch_id, issued_at DESC);

CREATE TABLE sales.invoice_items (
    id uuid PRIMARY KEY DEFAULT uuid_generate_v7(),
    invoice_id uuid NOT NULL REFERENCES sales.invoices(id) ON DELETE RESTRICT,
    sku varchar(100) NOT NULL,
    product_name varchar(200) NOT NULL,
    unit varchar(50) NOT NULL,
    quantity numeric(12, 3) NOT NULL,
    unit_price numeric(12, 2) NOT NULL,
    line_total numeric(12, 2) NOT NULL,
    CONSTRAINT ck_sales_invoice_items_quantity_positive CHECK (quantity > 0),
    CONSTRAINT ck_sales_invoice_items_unit_price_nonnegative CHECK (unit_price >= 0),
    CONSTRAINT ck_sales_invoice_items_line_total_nonnegative CHECK (line_total >= 0)
);
CREATE INDEX ix_sales_invoice_items_invoice_id ON sales.invoice_items (invoice_id);

CREATE TABLE auth.branch_user_roles (
    id uuid PRIMARY KEY DEFAULT uuid_generate_v7(),
    branch_id uuid NOT NULL REFERENCES core.branches(id) ON DELETE RESTRICT,
    user_id uuid NOT NULL REFERENCES auth.users(id) ON DELETE RESTRICT,
    role_id uuid NOT NULL REFERENCES auth.roles(id) ON DELETE RESTRICT,
    is_active boolean NOT NULL DEFAULT true,
    created_at timestamptz NOT NULL DEFAULT now(),
    created_by uuid NULL REFERENCES auth.users(id),
    updated_at timestamptz NOT NULL DEFAULT now(),
    updated_by uuid NULL REFERENCES auth.users(id),
    CONSTRAINT ux_branch_user_roles_branch_user UNIQUE (branch_id, user_id)
);
CREATE INDEX ix_branch_user_roles_user_id ON auth.branch_user_roles (user_id) WHERE is_active;

INSERT INTO auth.roles (id, code, name, description, is_active, created_at, updated_at)
SELECT uuid_generate_v7(), role_seed.code, role_seed.name, role_seed.description, true, now(), now()
FROM (VALUES
    ('BRANCH_ADMIN', 'Branch Administrator', 'Manages POS, catalog, and inventory for assigned branches.'),
    ('STAFF', 'Branch Staff', 'Uses POS and views sales and inventory for assigned branches.')
) AS role_seed(code, name, description)
WHERE NOT EXISTS (
    SELECT 1 FROM auth.roles role_existing
    WHERE role_existing.code = role_seed.code AND role_existing.deleted_at IS NULL
);

INSERT INTO auth.permissions (id, code, name, description, created_at, updated_at)
VALUES
    (uuid_generate_v7(), 'sales.view', 'View sales', 'View branch-scoped sales orders.', now(), now()),
    (uuid_generate_v7(), 'sales.create', 'Create sales', 'Create branch-scoped POS orders.', now(), now()),
    (uuid_generate_v7(), 'sales.cancel', 'Cancel sales', 'Cancel completed sales orders.', now(), now()),
    (uuid_generate_v7(), 'sales.refund', 'Refund sales', 'Record manual refunds for sales orders.', now(), now()),
    (uuid_generate_v7(), 'sales.invoice.view', 'View invoices', 'View branch-scoped invoices.', now(), now()),
    (uuid_generate_v7(), 'sales.invoice.print', 'Print invoices', 'Download or print branch-scoped invoices.', now(), now()),
    (uuid_generate_v7(), 'product.view', 'View products', 'View catalog products.', now(), now()),
    (uuid_generate_v7(), 'product.create', 'Create products', 'Create catalog products and categories.', now(), now()),
    (uuid_generate_v7(), 'product.update', 'Update products', 'Update catalog products and branch pricing.', now(), now()),
    (uuid_generate_v7(), 'product.deactivate', 'Deactivate products', 'Deactivate products and categories.', now(), now()),
    (uuid_generate_v7(), 'inventory.view', 'View inventory', 'View branch inventory and stock movements.', now(), now()),
    (uuid_generate_v7(), 'inventory.receive', 'Receive inventory', 'Create and edit goods receipt drafts.', now(), now()),
    (uuid_generate_v7(), 'inventory.post_receipt', 'Post goods receipts', 'Post branch goods receipts.', now(), now()),
    (uuid_generate_v7(), 'inventory.adjust', 'Adjust inventory', 'Adjust branch inventory with an audit movement.', now(), now()),
    (uuid_generate_v7(), 'sales.report.view', 'View sales reports', 'View branch-scoped financial reports.', now(), now())
ON CONFLICT (code) DO NOTHING;

INSERT INTO auth.role_permissions (role_id, permission_id)
SELECT role_row.id, permission_row.id
FROM auth.roles role_row
CROSS JOIN auth.permissions permission_row
WHERE role_row.code IN ('ADMIN', 'SUPER_ADMIN', 'BRANCH_ADMIN')
  AND role_row.deleted_at IS NULL
  AND permission_row.code IN (
      'sales.view', 'sales.create', 'sales.cancel', 'sales.refund',
      'sales.invoice.view', 'sales.invoice.print',
      'product.view', 'product.create', 'product.update', 'product.deactivate',
      'inventory.view', 'inventory.receive', 'inventory.post_receipt', 'inventory.adjust',
      'sales.report.view'
  )
ON CONFLICT (role_id, permission_id) DO NOTHING;

INSERT INTO auth.role_permissions (role_id, permission_id)
SELECT role_row.id, permission_row.id
FROM auth.roles role_row
CROSS JOIN auth.permissions permission_row
WHERE role_row.code = 'STAFF'
  AND role_row.deleted_at IS NULL
  AND permission_row.code IN (
      'sales.view', 'sales.create', 'sales.invoice.view', 'sales.invoice.print',
      'product.view', 'inventory.view'
  )
ON CONFLICT (role_id, permission_id) DO NOTHING;

COMMIT;
