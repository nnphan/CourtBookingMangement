BEGIN;

ALTER TABLE booking.bookings
    ADD COLUMN hold_token_hash bytea NULL;

COMMIT;
