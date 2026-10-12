BEGIN;

ALTER TABLE booking.bookings
    ALTER COLUMN booking_type TYPE VARCHAR(50),
    ALTER COLUMN booking_type SET DEFAULT 'Fixed';

UPDATE booking.bookings
SET booking_type = CASE LOWER(booking_type)
    WHEN 'instant' THEN 'Fixed'
    WHEN 'fixed' THEN 'Fixed'
    WHEN 'flexible' THEN 'Flexible'
    ELSE booking_type
END
WHERE LOWER(booking_type) IN ('instant', 'fixed', 'flexible');

ALTER TABLE booking.bookings
    ADD COLUMN IF NOT EXISTS payment_method VARCHAR(50) NOT NULL DEFAULT 'Cash';

COMMIT;