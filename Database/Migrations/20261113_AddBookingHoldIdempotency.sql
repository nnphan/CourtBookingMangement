BEGIN;

ALTER TABLE booking.bookings
    ADD COLUMN idempotency_key uuid NULL,
    ADD COLUMN idempotency_request_hash bytea NULL;

CREATE UNIQUE INDEX ux_bookings_idempotency_key
    ON booking.bookings (idempotency_key)
    WHERE idempotency_key IS NOT NULL;

COMMIT;
