BEGIN;

ALTER TABLE payment.payment_transactions
    DROP CONSTRAINT ck_payment_transactions_provider,
    DROP CONSTRAINT ck_payment_transactions_status;

ALTER TABLE payment.payment_transactions
    ADD CONSTRAINT ck_payment_transactions_provider
        CHECK (provider IN ('MOMO', 'SEPAY')),
    ADD CONSTRAINT ck_payment_transactions_status
        CHECK (
            status IN (
                'PENDING', 'PROCESSING', 'PAID', 'FAILED', 'EXPIRED',
                'PAYMENT_REVIEW', 'REFUND_PENDING', 'REFUNDED',
                'UNDERPAID', 'OVERPAID'
            )
        ),
    ADD COLUMN payment_code varchar(32) NULL,
    ADD COLUMN received_amount numeric(12, 2) NOT NULL DEFAULT 0,
    ADD COLUMN bank_reference_code varchar(100) NULL,
    ADD COLUMN bank_code varchar(50) NULL,
    ADD COLUMN bank_account_number varchar(64) NULL,
    ADD COLUMN expires_at timestamptz NULL,
    ADD CONSTRAINT ck_payment_transactions_sepay_amount
        CHECK (provider <> 'SEPAY' OR amount > 0),
    ADD CONSTRAINT ck_payment_transactions_received_amount
        CHECK (received_amount >= 0),
    ADD CONSTRAINT ck_payment_transactions_payment_code
        CHECK (payment_code IS NULL OR payment_code ~ '^[A-Z0-9]{6,32}$');

CREATE UNIQUE INDEX ux_payment_transactions_payment_code
    ON payment.payment_transactions (payment_code)
    WHERE payment_code IS NOT NULL;

CREATE INDEX ix_payment_transactions_status_expires_at
    ON payment.payment_transactions (status, expires_at)
    WHERE expires_at IS NOT NULL;

CREATE INDEX ix_payment_transactions_provider_status
    ON payment.payment_transactions (provider, status);

ALTER TABLE payment.payment_callback_logs
    DROP CONSTRAINT ck_payment_callback_logs_provider;

ALTER TABLE payment.payment_callback_logs
    ADD CONSTRAINT ck_payment_callback_logs_provider
        CHECK (provider IN ('MOMO', 'SEPAY')),
    ADD COLUMN sepay_transaction_id bigint NULL,
    ADD COLUMN gateway varchar(100) NULL,
    ADD COLUMN transaction_date timestamptz NULL,
    ADD COLUMN account_number varchar(64) NULL,
    ADD COLUMN sub_account varchar(100) NULL,
    ADD COLUMN payment_code varchar(32) NULL,
    ADD COLUMN transfer_type varchar(20) NULL,
    ADD COLUMN transfer_amount numeric(12, 2) NULL,
    ADD COLUMN reference_code varchar(100) NULL,
    ADD CONSTRAINT ck_payment_callback_logs_transfer_amount
        CHECK (transfer_amount IS NULL OR transfer_amount >= 0);

CREATE UNIQUE INDEX ux_payment_callback_logs_sepay_transaction_id
    ON payment.payment_callback_logs (sepay_transaction_id)
    WHERE provider = 'SEPAY' AND sepay_transaction_id IS NOT NULL;

COMMIT;
