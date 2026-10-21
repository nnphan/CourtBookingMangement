BEGIN;

ALTER TABLE matching.player_matches
    ADD CONSTRAINT player_matches_current_players_nonnegative_check
    CHECK (current_players >= 0);

ALTER TABLE matching.player_matches
    ADD CONSTRAINT player_matches_current_players_max_check
    CHECK (current_players <= max_players);

COMMIT;

-- dotnet run --launch-profile https