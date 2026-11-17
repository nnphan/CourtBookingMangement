BEGIN;

CREATE EXTENSION IF NOT EXISTS btree_gist;

ALTER TABLE booking.bookings
    ADD COLUMN expires_at timestamptz NULL,
    ADD COLUMN confirmed_at timestamptz NULL,
    ADD CONSTRAINT ck_bookings_expiration_after_creation
        CHECK (expires_at IS NULL OR expires_at > created_at);

CREATE INDEX ix_bookings_pending_payment_expiration
    ON booking.bookings (expires_at, id)
    WHERE status = 'pending_payment' AND expires_at IS NOT NULL;

UPDATE booking.booking_details AS detail
SET slot_range = tstzrange(
    (detail.booking_date + detail.start_time) AT TIME ZONE COALESCE(branch.time_zone, 'Asia/Ho_Chi_Minh'),
    (detail.booking_date + detail.end_time) AT TIME ZONE COALESCE(branch.time_zone, 'Asia/Ho_Chi_Minh'),
    '[)'
)
FROM booking.bookings AS booking
JOIN core.branches AS branch ON branch.id = booking.branch_id
WHERE detail.booking_id = booking.id
  AND detail.slot_range IS NULL;

UPDATE booking.bookings
SET confirmed_at = created_at
WHERE confirmed_at IS NULL
  AND LOWER(status) IN ('confirmed', 'checked_in');

UPDATE booking.booking_details AS detail
SET status = CASE
    WHEN LOWER(booking.status) = 'pending_payment' THEN 'held'
    WHEN LOWER(booking.status) IN ('cancelled', 'expired') THEN LOWER(booking.status)
    WHEN NOT booking.is_active OR booking.deleted_at IS NOT NULL THEN 'cancelled'
    WHEN LOWER(booking.status) = 'completed' THEN 'completed'
    ELSE 'confirmed'
END
FROM booking.bookings AS booking
WHERE booking.id = detail.booking_id;

ALTER TABLE booking.booking_details
    ALTER COLUMN slot_range SET NOT NULL;

ALTER TABLE booking.booking_details
    ADD CONSTRAINT booking_details_active_slot_no_overlap
    EXCLUDE USING gist (
        court_id WITH =,
        slot_range WITH &&
    )
    WHERE (UPPER(status) IN ('HELD', 'CONFIRMED'));

CREATE TABLE payment.payment_transactions (
    id uuid PRIMARY KEY DEFAULT uuid_generate_v7(),
    booking_id uuid NOT NULL REFERENCES booking.bookings(id) ON DELETE RESTRICT,
    provider varchar(30) NOT NULL,
    payment_method varchar(30) NOT NULL,
    order_id varchar(100) NOT NULL,
    request_id varchar(100) NOT NULL,
    provider_transaction_id varchar(100) NULL,
    amount numeric(12, 2) NOT NULL,
    currency char(3) NOT NULL DEFAULT 'VND',
    status varchar(30) NOT NULL,
    result_code varchar(30) NULL,
    provider_message varchar(500) NULL,
    pay_url text NULL,
    deeplink text NULL,
    qr_code_url text NULL,
    signature_verified boolean NOT NULL DEFAULT false,
    paid_at timestamptz NULL,
    expired_at timestamptz NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ux_payment_transactions_order_id UNIQUE (order_id),
    CONSTRAINT ux_payment_transactions_request_id UNIQUE (request_id),
    CONSTRAINT ck_payment_transactions_amount_nonnegative CHECK (amount >= 0),
    CONSTRAINT ck_payment_transactions_provider CHECK (provider IN ('MOMO')),
    CONSTRAINT ck_payment_transactions_status CHECK (
        status IN (
            'PENDING', 'PROCESSING', 'PAID', 'FAILED', 'EXPIRED',
            'PAYMENT_REVIEW', 'REFUND_PENDING', 'REFUNDED'
        )
    )
);

CREATE INDEX ix_payment_transactions_booking_status
    ON payment.payment_transactions (booking_id, status, created_at DESC);

CREATE UNIQUE INDEX ux_payment_transactions_provider_transaction_id
    ON payment.payment_transactions (provider, provider_transaction_id)
    WHERE provider_transaction_id IS NOT NULL;

CREATE TABLE payment.payment_callback_logs (
    id uuid PRIMARY KEY DEFAULT uuid_generate_v7(),
    provider varchar(30) NOT NULL,
    order_id varchar(100) NULL,
    request_id varchar(100) NULL,
    signature_verified boolean NOT NULL DEFAULT false,
    payload jsonb NOT NULL,
    processing_status varchar(30) NOT NULL,
    processing_message varchar(500) NULL,
    received_at timestamptz NOT NULL DEFAULT now(),
    processed_at timestamptz NULL,
    CONSTRAINT ck_payment_callback_logs_provider CHECK (provider IN ('MOMO')),
    CONSTRAINT ck_payment_callback_logs_status CHECK (
        processing_status IN ('RECEIVED', 'PROCESSED', 'REJECTED', 'REQUIRES_REVIEW')
    )
);

CREATE INDEX ix_payment_callback_logs_order_received
    ON payment.payment_callback_logs (order_id, received_at DESC)
    WHERE order_id IS NOT NULL;

COMMIT;
