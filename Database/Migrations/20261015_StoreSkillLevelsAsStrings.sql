BEGIN;

ALTER TABLE matching.player_matches
    ALTER COLUMN skill_level DROP DEFAULT;

ALTER TABLE matching.player_matches
    ALTER COLUMN skill_level TYPE VARCHAR(30)
    USING CASE UPPER(skill_level::text)
        WHEN '0' THEN 'BEGINNER'
        WHEN '1' THEN 'INTERMEDIATE'
        WHEN '2' THEN 'ADVANCED'
        WHEN '3' THEN 'PROFESSIONAL'
        WHEN '4' THEN 'PROFESSIONAL'
        WHEN 'BEGINNER' THEN 'BEGINNER'
        WHEN 'INTERMEDIATE' THEN 'INTERMEDIATE'
        WHEN 'ADVANCED' THEN 'ADVANCED'
        WHEN 'PRO' THEN 'PROFESSIONAL'
        WHEN 'PROFESSIONAL' THEN 'PROFESSIONAL'
        ELSE UPPER(skill_level::text)
    END;

ALTER TABLE matching.player_matches
    ALTER COLUMN skill_level SET DEFAULT 'BEGINNER',
    ADD CONSTRAINT ck_player_matches_skill_level
        CHECK (skill_level IN ('BEGINNER', 'INTERMEDIATE', 'ADVANCED', 'PROFESSIONAL'));

COMMIT;