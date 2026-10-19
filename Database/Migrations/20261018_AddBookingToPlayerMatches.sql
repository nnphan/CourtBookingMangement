BEGIN;

ALTER TABLE matching.player_matches
    ADD COLUMN booking_id uuid NULL;

ALTER TABLE matching.player_matches
    ADD CONSTRAINT player_matches_booking_id_fkey
    FOREIGN KEY (booking_id)
    REFERENCES booking.bookings (id)
    ON DELETE RESTRICT;

CREATE INDEX idx_player_matches_booking_id
    ON matching.player_matches (booking_id);

COMMIT;