BEGIN;

DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM auth.users
        WHERE deleted_at IS NULL
        GROUP BY lower(email)
        HAVING COUNT(*) > 1
    ) THEN
        RAISE EXCEPTION 'Cannot enforce case-insensitive email uniqueness: duplicate active emails exist.';
    END IF;
END $$;

CREATE UNIQUE INDEX IF NOT EXISTS ux_users_normalized_email
    ON auth.users (lower(email))
    WHERE deleted_at IS NULL;

COMMIT;
