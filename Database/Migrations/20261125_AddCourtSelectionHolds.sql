BEGIN;

CREATE TABLE booking.court_selection_holds (
    id uuid PRIMARY KEY,
    branch_id uuid NOT NULL REFERENCES core.branches(id),
    business_date date NOT NULL,
    subject_id uuid NOT NULL,
    user_id uuid REFERENCES auth.users(id),
    session_id uuid NOT NULL,
    expires_at timestamptz NOT NULL,
    closed_at timestamptz,
    booking_id uuid UNIQUE REFERENCES booking.bookings(id),
    CHECK (user_id IS NULL OR user_id = subject_id)
);

CREATE UNIQUE INDEX ux_court_selection_open_session
    ON booking.court_selection_holds(subject_id, session_id)
    WHERE closed_at IS NULL;
CREATE INDEX ix_court_selection_expiration
    ON booking.court_selection_holds(expires_at) WHERE closed_at IS NULL;
CREATE INDEX ix_court_selection_board
    ON booking.court_selection_holds(branch_id, business_date) WHERE closed_at IS NULL;

CREATE TABLE booking.court_selection_slots (
    id uuid PRIMARY KEY,
    hold_id uuid NOT NULL REFERENCES booking.court_selection_holds(id) ON DELETE CASCADE,
    court_id uuid NOT NULL REFERENCES core.courts(id),
    slot_range tstzrange NOT NULL CHECK (NOT isempty(slot_range)),
    UNIQUE (hold_id, court_id, slot_range)
);
CREATE INDEX ix_court_selection_slot_range
    ON booking.court_selection_slots USING gist(court_id, slot_range);

CREATE TABLE booking.court_selection_operations (
    subject_id uuid NOT NULL,
    idempotency_key uuid NOT NULL,
    request_hash text NOT NULL,
    response jsonb NOT NULL,
    retain_until timestamptz NOT NULL,
    PRIMARY KEY (subject_id, idempotency_key)
);
CREATE INDEX ix_court_selection_operation_retention
    ON booking.court_selection_operations(retain_until);

-- Shares BookingRepository's court/date lock with selection claims and covers
-- booking-detail writers that do not go through that repository.
CREATE FUNCTION booking.check_court_selection_overlap() RETURNS trigger
LANGUAGE plpgsql AS $$
BEGIN
    IF upper(NEW.status) NOT IN ('HELD', 'CONFIRMED', 'RESERVED') THEN
        RETURN NEW;
    END IF;
    IF TG_OP = 'UPDATE' THEN
        IF upper(OLD.status) IN ('HELD', 'CONFIRMED', 'RESERVED')
           AND (OLD.court_id, OLD.booking_date, OLD.slot_range)
               IS NOT DISTINCT FROM (NEW.court_id, NEW.booking_date, NEW.slot_range) THEN
            RETURN NEW;
        END IF;
    END IF;
    PERFORM pg_advisory_xact_lock(hashtextextended(
        NEW.court_id::text || ':' || to_char(NEW.booking_date, 'YYYY-MM-DD'), 0));
    IF EXISTS (
        SELECT 1 FROM booking.court_selection_slots s
        JOIN booking.court_selection_holds h ON h.id = s.hold_id
        WHERE s.court_id = NEW.court_id AND s.slot_range && NEW.slot_range
          AND h.closed_at IS NULL AND h.expires_at > clock_timestamp()
    ) THEN
        RAISE exclusion_violation USING MESSAGE = 'Court range is held by a selection session';
    END IF;
    RETURN NEW;
END;
$$;

CREATE TRIGGER check_court_selection_overlap
BEFORE INSERT OR UPDATE OF status, court_id, booking_date, slot_range
ON booking.booking_details
FOR EACH ROW EXECUTE FUNCTION booking.check_court_selection_overlap();

COMMIT;
