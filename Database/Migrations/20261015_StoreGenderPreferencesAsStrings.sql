BEGIN;

ALTER TABLE matching.player_matches
    ALTER COLUMN gender_preference DROP DEFAULT;

ALTER TABLE matching.player_matches
    ALTER COLUMN gender_preference TYPE VARCHAR(20)
    USING CASE UPPER(gender_preference::text)
        WHEN '0' THEN 'MALE'
        WHEN '1' THEN 'FEMALE'
        WHEN '2' THEN 'BOTH'
        WHEN 'MALE' THEN 'MALE'
        WHEN 'FEMALE' THEN 'FEMALE'
        WHEN 'BOTH' THEN 'BOTH'
        ELSE UPPER(gender_preference::text)
    END;

UPDATE matching.player_matches
SET gender_preference = 'BOTH'
WHERE gender_preference IS NULL;

ALTER TABLE matching.player_matches
    ALTER COLUMN gender_preference SET DEFAULT 'BOTH',
    ALTER COLUMN gender_preference SET NOT NULL,
    ADD CONSTRAINT ck_player_matches_gender_preference
        CHECK (gender_preference IN ('MALE', 'FEMALE', 'BOTH'));

COMMIT;