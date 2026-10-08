BEGIN;

ALTER TABLE core.branches
    ADD COLUMN IF NOT EXISTS description varchar(1000);

COMMIT;