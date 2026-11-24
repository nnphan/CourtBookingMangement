BEGIN;

DO $$
BEGIN
    IF to_regclass('auth.staff_invitations') IS NULL THEN
        RAISE EXCEPTION
            'Cannot upgrade Staff invitation tokens: auth.staff_invitations is missing. Apply the Staff Management migration first.';
    END IF;
END $$;

ALTER TABLE auth.staff_invitations
    ADD COLUMN IF NOT EXISTS link_version smallint NOT NULL DEFAULT 1,
    ADD COLUMN IF NOT EXISTS issuance_type varchar(12) NULL;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'ck_staff_invitations_link_version'
          AND conrelid = 'auth.staff_invitations'::regclass
    ) THEN
        ALTER TABLE auth.staff_invitations
            ADD CONSTRAINT ck_staff_invitations_link_version
            CHECK (link_version IN (1, 2));
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'ck_staff_invitations_issuance_type'
          AND conrelid = 'auth.staff_invitations'::regclass
    ) THEN
        ALTER TABLE auth.staff_invitations
            ADD CONSTRAINT ck_staff_invitations_issuance_type
            CHECK (issuance_type IS NULL OR issuance_type IN ('CREATE', 'RESEND'));
    END IF;
END $$;

CREATE UNIQUE INDEX IF NOT EXISTS ux_staff_invitations_token_hash
    ON auth.staff_invitations (token_hash);

CREATE UNIQUE INDEX IF NOT EXISTS ux_staff_invitations_outstanding_user_purpose
    ON auth.staff_invitations (user_id, purpose)
    WHERE used_at IS NULL
      AND revoked_at IS NULL;

CREATE INDEX IF NOT EXISTS ix_staff_invitations_resend_window
    ON auth.staff_invitations (user_id, created_at DESC)
    WHERE purpose = 'STAFF_ACTIVATION'
      AND issuance_type = 'RESEND';

COMMENT ON COLUMN auth.staff_invitations.link_version IS
    '1 = legacy hex fragment link; 2 = email-bound Base64URL query link.';

COMMENT ON COLUMN auth.staff_invitations.issuance_type IS
    'CREATE or RESEND when known; NULL for legacy rows whose issuance origin is unknown.';

COMMIT;
