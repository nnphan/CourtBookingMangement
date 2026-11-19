BEGIN;

DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM matching.player_matches
        WHERE status NOT IN ('DRAFT', 'OPEN', 'FULL', 'STARTED', 'COMPLETED', 'CANCELLED', 'EXPIRED')
    ) THEN
        RAISE EXCEPTION 'Cannot add player match status constraint: inspect unsupported existing room statuses.';
    END IF;

    IF EXISTS (
        SELECT 1 FROM matching.match_join_requests
        WHERE status NOT IN ('PENDING', 'APPROVED', 'REJECTED', 'CANCELLED', 'EXPIRED')
    ) THEN
        RAISE EXCEPTION 'Cannot add join request status constraint: inspect unsupported existing request statuses.';
    END IF;

    IF EXISTS (
        SELECT 1 FROM matching.match_participants
        WHERE status = 'ACTIVE'
        GROUP BY match_id, user_id
        HAVING COUNT(*) > 1
    ) THEN
        RAISE EXCEPTION 'Cannot add active membership index: resolve duplicate active participants.';
    END IF;

    IF EXISTS (
        SELECT 1 FROM matching.match_join_requests
        WHERE status = 'PENDING'
        GROUP BY match_id, requester_id
        HAVING COUNT(*) > 1
    ) THEN
        RAISE EXCEPTION 'Cannot add pending request index: resolve duplicate pending requests.';
    END IF;
END
$$;

CREATE TABLE matching.player_profiles (
    id uuid PRIMARY KEY DEFAULT uuid_generate_v7(),
    user_id uuid NOT NULL UNIQUE REFERENCES auth.users(id) ON DELETE RESTRICT,
    current_rating integer NOT NULL CHECK (current_rating BETWEEN 0 AND 1000),
    rating_confidence numeric(5, 4) NOT NULL DEFAULT 0.4000
        CHECK (rating_confidence BETWEEN 0 AND 1),
    rating_source varchar(30) NOT NULL DEFAULT 'SELF_ASSESSMENT'
        CHECK (rating_source IN ('SELF_ASSESSMENT', 'HOST_REVIEW', 'PEER_REVIEW',
                                 'MATCH_RESULT', 'ADMIN_ASSESSMENT', 'SYSTEM_CALCULATED')),
    matches_played integer NOT NULL DEFAULT 0 CHECK (matches_played >= 0),
    last_evaluated_at timestamptz NULL,
    gender varchar(10) NULL CHECK (gender IN ('MALE', 'FEMALE', 'OTHER')),
    preferred_game_type varchar(10) NULL CHECK (preferred_game_type IN ('SINGLES', 'DOUBLES')),
    preferred_play_style varchar(50) NULL,
    is_profile_public boolean NOT NULL DEFAULT false,
    is_matching_enabled boolean NOT NULL DEFAULT true,
    created_at timestamptz NOT NULL DEFAULT now(),
    created_by uuid NULL REFERENCES auth.users(id) ON DELETE RESTRICT,
    updated_at timestamptz NOT NULL DEFAULT now(),
    updated_by uuid NULL REFERENCES auth.users(id) ON DELETE RESTRICT
);

CREATE TABLE matching.player_rating_history (
    id uuid PRIMARY KEY DEFAULT uuid_generate_v7(),
    player_profile_id uuid NOT NULL REFERENCES matching.player_profiles(id) ON DELETE RESTRICT,
    old_rating integer NULL CHECK (old_rating BETWEEN 0 AND 1000),
    new_rating integer NOT NULL CHECK (new_rating BETWEEN 0 AND 1000),
    old_confidence numeric(5, 4) NULL CHECK (old_confidence BETWEEN 0 AND 1),
    new_confidence numeric(5, 4) NOT NULL CHECK (new_confidence BETWEEN 0 AND 1),
    source varchar(30) NOT NULL CHECK (source IN ('SELF_ASSESSMENT', 'HOST_REVIEW', 'PEER_REVIEW',
                                                  'MATCH_RESULT', 'ADMIN_ASSESSMENT', 'SYSTEM_CALCULATED')),
    reference_type varchar(50) NULL,
    reference_id uuid NULL,
    notes text NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    created_by uuid NULL REFERENCES auth.users(id) ON DELETE RESTRICT
);

CREATE INDEX ix_player_rating_history_profile_created
    ON matching.player_rating_history (player_profile_id, created_at DESC);

CREATE FUNCTION matching.record_player_rating_history()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    IF TG_OP = 'INSERT' THEN
        INSERT INTO matching.player_rating_history
            (player_profile_id, old_rating, new_rating, old_confidence, new_confidence, source, created_by)
        VALUES
            (NEW.id, NULL, NEW.current_rating, NULL, NEW.rating_confidence, NEW.rating_source, NEW.created_by);
    ELSIF OLD.current_rating IS DISTINCT FROM NEW.current_rating
       OR OLD.rating_confidence IS DISTINCT FROM NEW.rating_confidence THEN
        INSERT INTO matching.player_rating_history
            (player_profile_id, old_rating, new_rating, old_confidence, new_confidence, source, created_by)
        VALUES
            (NEW.id, OLD.current_rating, NEW.current_rating, OLD.rating_confidence,
             NEW.rating_confidence, NEW.rating_source, NEW.updated_by);
    END IF;
    RETURN NEW;
END;
$$;

CREATE TRIGGER trg_player_profile_rating_history
AFTER INSERT OR UPDATE OF current_rating, rating_confidence ON matching.player_profiles
FOR EACH ROW EXECUTE FUNCTION matching.record_player_rating_history();

CREATE TABLE matching.player_match_preferences (
    id uuid PRIMARY KEY DEFAULT uuid_generate_v7(),
    player_profile_id uuid NOT NULL UNIQUE REFERENCES matching.player_profiles(id) ON DELETE RESTRICT,
    skill_weight numeric NOT NULL DEFAULT 50 CHECK (skill_weight BETWEEN 0 AND 100),
    time_weight numeric NOT NULL DEFAULT 20 CHECK (time_weight BETWEEN 0 AND 100),
    branch_weight numeric NOT NULL DEFAULT 20 CHECK (branch_weight BETWEEN 0 AND 100),
    gender_weight numeric NOT NULL DEFAULT 10 CHECK (gender_weight BETWEEN 0 AND 100),
    preferred_start_time time NULL,
    preferred_end_time time NULL,
    minimum_time_overlap_minutes integer NOT NULL DEFAULT 60 CHECK (minimum_time_overlap_minutes > 0),
    maximum_participation_fee numeric(12, 2) NULL CHECK (maximum_participation_fee >= 0),
    updated_at timestamptz NOT NULL DEFAULT now(),
    CHECK (skill_weight + time_weight + branch_weight + gender_weight = 100),
    CHECK (
        (preferred_start_time IS NULL AND preferred_end_time IS NULL)
        OR (preferred_start_time IS NOT NULL AND preferred_end_time IS NOT NULL
            AND preferred_start_time < preferred_end_time
            AND minimum_time_overlap_minutes * INTERVAL '1 minute'
                <= preferred_end_time - preferred_start_time)
    )
);

CREATE TABLE matching.player_preferred_branches (
    player_profile_id uuid NOT NULL REFERENCES matching.player_profiles(id) ON DELETE RESTRICT,
    branch_id uuid NOT NULL REFERENCES core.branches(id) ON DELETE RESTRICT,
    priority_order integer NOT NULL CHECK (priority_order >= 0),
    is_primary boolean GENERATED ALWAYS AS (priority_order = 0) STORED,
    PRIMARY KEY (player_profile_id, branch_id),
    UNIQUE (player_profile_id, priority_order)
);

ALTER TABLE matching.player_matches
    ALTER COLUMN booking_id DROP NOT NULL,
    ALTER COLUMN match_date DROP NOT NULL,
    ALTER COLUMN start_time DROP NOT NULL,
    ALTER COLUMN end_time DROP NOT NULL,
    ADD COLUMN room_code text GENERATED ALWAYS AS ('MR' || replace(id::text, '-', '')) STORED,
    ADD COLUMN game_type varchar(10) NULL CHECK (game_type IN ('SINGLES', 'DOUBLES')),
    ADD COLUMN minimum_rating integer NULL CHECK (minimum_rating BETWEEN 0 AND 1000),
    ADD COLUMN maximum_rating integer NULL CHECK (maximum_rating BETWEEN 0 AND 1000),
    ADD COLUMN gender_policy varchar(10) NULL CHECK (gender_policy IN ('ANY', 'SPECIFIC')),
    ADD COLUMN required_gender varchar(10) NULL CHECK (required_gender IN ('MALE', 'FEMALE', 'OTHER')),
    ADD COLUMN approval_mode varchar(20) NOT NULL DEFAULT 'MANUAL'
        CHECK (approval_mode IN ('MANUAL', 'AUTO_APPROVE')),
    ADD COLUMN visibility varchar(20) NOT NULL DEFAULT 'PUBLIC'
        CHECK (visibility IN ('PUBLIC', 'PRIVATE')),
    ADD COLUMN cancelled_at timestamptz NULL,
    ADD COLUMN cancelled_by uuid NULL REFERENCES auth.users(id) ON DELETE RESTRICT,
    ADD CONSTRAINT ck_player_matches_new_status
        CHECK (status IN ('DRAFT', 'OPEN', 'FULL', 'STARTED', 'COMPLETED', 'CANCELLED', 'EXPIRED')),
    ADD CONSTRAINT ck_player_matches_rating_range CHECK (
        (minimum_rating IS NULL AND maximum_rating IS NULL)
        OR (minimum_rating IS NOT NULL AND maximum_rating IS NOT NULL AND minimum_rating <= maximum_rating)
    ),
    ADD CONSTRAINT ck_player_matches_gender_policy CHECK (
        CASE
            WHEN gender_policy IS NULL OR gender_policy = 'ANY' THEN required_gender IS NULL
            WHEN gender_policy = 'SPECIFIC' THEN required_gender IS NOT NULL
            ELSE false
        END
    ),
    ADD CONSTRAINT ck_player_matches_new_schedule CHECK (
        start_time IS NULL OR end_time IS NULL OR start_time < end_time
    ) NOT VALID,
    ADD CONSTRAINT ck_player_matches_new_fee CHECK (
        fee_per_player IS NULL OR fee_per_player >= 0
    ) NOT VALID;

CREATE UNIQUE INDEX ux_player_matches_room_code ON matching.player_matches (room_code);
CREATE INDEX ix_player_matches_discovery ON matching.player_matches (status, match_date, start_time, id);
CREATE INDEX ix_player_matches_branch_discovery ON matching.player_matches (branch_id, match_date, status);

ALTER TABLE matching.match_join_requests
    ADD COLUMN rejection_reason varchar(500) NULL,
    ADD COLUMN updated_at timestamptz NOT NULL DEFAULT now(),
    ADD CONSTRAINT ck_match_join_requests_new_status
        CHECK (status IN ('PENDING', 'APPROVED', 'REJECTED', 'CANCELLED', 'EXPIRED'));

ALTER TABLE matching.match_join_requests DROP CONSTRAINT IF EXISTS uq_join_request;
DROP INDEX IF EXISTS matching.uq_join_request;
CREATE UNIQUE INDEX ux_match_join_requests_pending
    ON matching.match_join_requests (match_id, requester_id) WHERE status = 'PENDING';
CREATE INDEX ix_match_join_requests_match_status ON matching.match_join_requests (match_id, status);
CREATE INDEX ix_match_join_requests_requester_status ON matching.match_join_requests (requester_id, status);

ALTER TABLE matching.match_participants
    ADD COLUMN left_at timestamptz NULL;
ALTER TABLE matching.match_participants DROP CONSTRAINT IF EXISTS uq_match_participant;
DROP INDEX IF EXISTS matching.uq_match_participant;
CREATE UNIQUE INDEX ux_match_participants_active
    ON matching.match_participants (match_id, user_id) WHERE status = 'ACTIVE';
CREATE INDEX ix_match_participants_match_status ON matching.match_participants (match_id, status);
CREATE INDEX ix_match_participants_user_status ON matching.match_participants (user_id, status);

COMMIT;
