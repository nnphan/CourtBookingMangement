BEGIN;

ALTER TABLE matching.player_match_preferences
    ALTER COLUMN skill_weight SET DEFAULT 40,
    ALTER COLUMN time_weight SET DEFAULT 30;

COMMIT;
