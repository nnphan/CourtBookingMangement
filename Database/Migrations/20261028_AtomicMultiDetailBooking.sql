BEGIN;

CREATE EXTENSION IF NOT EXISTS btree_gist;

UPDATE booking.booking_details AS detail
SET status = CASE
    WHEN LOWER(booking.status) = 'completed' THEN 'completed'
    WHEN LOWER(booking.status) = 'expired' THEN 'expired'
    ELSE 'cancelled'
END
FROM booking.bookings AS booking
WHERE booking.id = detail.booking_id
  AND (
      NOT booking.is_active
      OR booking.deleted_at IS NOT NULL
      OR LOWER(booking.status) IN ('cancelled', 'expired', 'completed')
  )
  AND LOWER(detail.status) IN ('reserved', 'confirmed');

UPDATE booking.booking_details AS detail
SET slot_range = tstzrange(
    (detail.booking_date + detail.start_time) AT TIME ZONE branch.time_zone,
    (detail.booking_date + detail.end_time) AT TIME ZONE branch.time_zone,
    '[)'
)
FROM booking.bookings AS booking
JOIN core.branches AS branch ON branch.id = booking.branch_id
WHERE booking.id = detail.booking_id;

CREATE OR REPLACE FUNCTION booking.set_booking_detail_slot_range()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
    branch_time_zone text;
BEGIN
    SELECT branch.time_zone
    INTO branch_time_zone
    FROM booking.bookings AS booking
    JOIN core.branches AS branch ON branch.id = booking.branch_id
    WHERE booking.id = NEW.booking_id;

    IF branch_time_zone IS NULL OR branch_time_zone = '' THEN
        RAISE EXCEPTION 'A booking branch timezone is required to create a reservation.'
            USING ERRCODE = '22023';
    END IF;

    NEW.slot_range := tstzrange(
        (NEW.booking_date + NEW.start_time) AT TIME ZONE branch_time_zone,
        (NEW.booking_date + NEW.end_time) AT TIME ZONE branch_time_zone,
        '[)'
    );
    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS booking_details_set_slot_range ON booking.booking_details;
CREATE TRIGGER booking_details_set_slot_range
BEFORE INSERT OR UPDATE ON booking.booking_details
FOR EACH ROW
EXECUTE FUNCTION booking.set_booking_detail_slot_range();

CREATE OR REPLACE FUNCTION booking.sync_detail_status_with_booking()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
    terminal_detail_status text;
BEGIN
    IF LOWER(NEW.status) = 'completed' THEN
        terminal_detail_status := 'completed';
    ELSIF LOWER(NEW.status) = 'expired' THEN
        terminal_detail_status := 'expired';
    ELSE
        terminal_detail_status := 'cancelled';
    END IF;

    UPDATE booking.booking_details
    SET status = terminal_detail_status
    WHERE booking_id = NEW.id
      AND LOWER(status) IN ('reserved', 'confirmed');

    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS bookings_sync_detail_status ON booking.bookings;
CREATE TRIGGER bookings_sync_detail_status
AFTER UPDATE OF status, is_active, deleted_at ON booking.bookings
FOR EACH ROW
WHEN (
    NOT NEW.is_active
    OR NEW.deleted_at IS NOT NULL
    OR LOWER(NEW.status) IN ('cancelled', 'expired', 'completed')
)
EXECUTE FUNCTION booking.sync_detail_status_with_booking();

ALTER TABLE booking.booking_details
    ALTER COLUMN slot_range SET NOT NULL;

DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM booking.booking_details AS first_detail
        JOIN booking.booking_details AS second_detail
          ON first_detail.id < second_detail.id
         AND first_detail.court_id = second_detail.court_id
         AND first_detail.slot_range && second_detail.slot_range
        WHERE LOWER(first_detail.status) IN ('reserved', 'confirmed')
          AND LOWER(second_detail.status) IN ('reserved', 'confirmed')
    ) THEN
        RAISE EXCEPTION 'Cannot add the booking overlap constraint while active overlapping reservations exist.';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint AS constraint_info
        WHERE constraint_info.conrelid = 'booking.booking_details'::regclass
          AND constraint_info.contype = 'x'
          AND pg_get_constraintdef(constraint_info.oid) ILIKE '%court_id%'
          AND pg_get_constraintdef(constraint_info.oid) ILIKE '%slot_range%'
          AND pg_get_constraintdef(constraint_info.oid) ILIKE '%&&%'
    ) THEN
        ALTER TABLE booking.booking_details
            ADD CONSTRAINT booking_details_court_slot_excl
            EXCLUDE USING gist
                (court_id WITH =, slot_range WITH &&)
            WHERE (LOWER(status) IN ('reserved', 'confirmed'));
    END IF;
END;
$$;

INSERT INTO auth.permissions (id, code, name, description, created_at, updated_at)
VALUES (
    gen_random_uuid(),
    'booking.create',
    'Create bookings for customers',
    'Allows authorized staff to create bookings on behalf of customers or guests.',
    NOW(),
    NOW()
)
ON CONFLICT (code) DO NOTHING;

COMMIT;
