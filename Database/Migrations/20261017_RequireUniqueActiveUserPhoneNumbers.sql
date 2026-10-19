BEGIN;

DO $$
DECLARE
    duplicate_phone_groups BIGINT;
BEGIN
    SELECT COUNT(*)
    INTO duplicate_phone_groups
    FROM
    (
        SELECT phone_number
        FROM auth.users
        WHERE phone_number IS NOT NULL
          AND deleted_at IS NULL
        GROUP BY phone_number
        HAVING COUNT(*) > 1
    ) duplicates;

    IF duplicate_phone_groups > 0 THEN
        RAISE EXCEPTION
            'Cannot enforce unique active user phone numbers: % duplicate groups exist. Resolve them manually before retrying.',
            duplicate_phone_groups;
    END IF;
END $$;

DROP INDEX IF EXISTS auth.ix_users_phone;

CREATE UNIQUE INDEX IF NOT EXISTS ux_users_active_phone_number
    ON auth.users(phone_number)
    WHERE deleted_at IS NULL
      AND phone_number IS NOT NULL;

COMMIT;