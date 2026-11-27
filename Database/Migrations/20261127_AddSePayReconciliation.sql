BEGIN;

ALTER TABLE payment.payment_callback_logs
    ADD COLUMN IF NOT EXISTS sepay_api_transaction_id uuid NULL;

CREATE UNIQUE INDEX IF NOT EXISTS ux_payment_callback_logs_sepay_api_transaction_id
    ON payment.payment_callback_logs (sepay_api_transaction_id)
    WHERE provider = 'SEPAY' AND sepay_api_transaction_id IS NOT NULL;

CREATE INDEX IF NOT EXISTS ix_payment_callback_logs_sepay_bank_reference
    ON payment.payment_callback_logs (payment_code, reference_code)
    WHERE provider = 'SEPAY' AND reference_code IS NOT NULL;

COMMIT;
