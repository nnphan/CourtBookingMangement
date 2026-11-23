BEGIN;

DO $$
BEGIN
    IF to_regclass('auth.branch_user_roles') IS NULL THEN
        RAISE EXCEPTION
            'Cannot add Branch Owner Staff Management: auth.branch_user_roles is missing. Apply the branch-scoped POS migration first.';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM auth.roles
        WHERE code = 'BRANCH_ADMIN'
          AND is_active
          AND deleted_at IS NULL
    ) THEN
        RAISE EXCEPTION
            'Cannot seed Staff Management permissions: an active BRANCH_ADMIN role is required by branch authorization.';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM auth.roles
        WHERE code = 'STAFF'
          AND is_active
          AND deleted_at IS NULL
    ) THEN
        RAISE EXCEPTION
            'Cannot add Branch Owner Staff Management: an active STAFF role is required for branch assignments.';
    END IF;
END $$;

ALTER TABLE auth.branch_user_roles
    ADD COLUMN IF NOT EXISTS staff_code varchar(50) NULL,
    ADD COLUMN IF NOT EXISTS job_title varchar(100) NULL,
    ADD COLUMN IF NOT EXISTS employment_start_date date NULL,
    ADD COLUMN IF NOT EXISTS notes varchar(1000) NULL,
    ADD COLUMN IF NOT EXISTS removed_at timestamptz NULL,
    ADD COLUMN IF NOT EXISTS removed_by uuid NULL;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'fk_branch_user_roles_removed_by'
          AND conrelid = 'auth.branch_user_roles'::regclass
    ) THEN
        ALTER TABLE auth.branch_user_roles
            ADD CONSTRAINT fk_branch_user_roles_removed_by
            FOREIGN KEY (removed_by)
            REFERENCES auth.users(id)
            ON DELETE RESTRICT;
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'ck_branch_user_roles_staff_code_not_blank'
          AND conrelid = 'auth.branch_user_roles'::regclass
    ) THEN
        ALTER TABLE auth.branch_user_roles
            ADD CONSTRAINT ck_branch_user_roles_staff_code_not_blank
            CHECK (staff_code IS NULL OR length(trim(staff_code)) > 0);
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'ck_branch_user_roles_job_title_not_blank'
          AND conrelid = 'auth.branch_user_roles'::regclass
    ) THEN
        ALTER TABLE auth.branch_user_roles
            ADD CONSTRAINT ck_branch_user_roles_job_title_not_blank
            CHECK (job_title IS NULL OR length(trim(job_title)) > 0);
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'ck_branch_user_roles_removed_inactive'
          AND conrelid = 'auth.branch_user_roles'::regclass
    ) THEN
        ALTER TABLE auth.branch_user_roles
            ADD CONSTRAINT ck_branch_user_roles_removed_inactive
            CHECK (removed_at IS NULL OR NOT is_active);
    END IF;
END $$;

CREATE UNIQUE INDEX IF NOT EXISTS ux_branch_user_roles_active_staff_code
    ON auth.branch_user_roles (branch_id, lower(staff_code))
    WHERE is_active
      AND removed_at IS NULL
      AND staff_code IS NOT NULL;

CREATE TABLE IF NOT EXISTS auth.staff_invitations (
    id uuid PRIMARY KEY DEFAULT uuid_generate_v7(),
    user_id uuid NOT NULL REFERENCES auth.users(id) ON DELETE RESTRICT,
    purpose varchar(32) NOT NULL,
    token_hash varchar(64) NOT NULL,
    expires_at timestamptz NOT NULL,
    used_at timestamptz NULL,
    revoked_at timestamptz NULL,
    delivery_status varchar(16) NOT NULL DEFAULT 'PENDING',
    delivery_error_code varchar(100) NULL,
    sent_at timestamptz NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    created_by uuid NULL REFERENCES auth.users(id) ON DELETE RESTRICT,
    updated_at timestamptz NOT NULL DEFAULT now(),
    updated_by uuid NULL REFERENCES auth.users(id) ON DELETE RESTRICT,
    CONSTRAINT ck_staff_invitations_purpose
        CHECK (purpose IN ('STAFF_ACTIVATION', 'STAFF_PASSWORD_RESET')),
    CONSTRAINT ck_staff_invitations_delivery_status
        CHECK (delivery_status IN ('PENDING', 'SENT', 'FAILED')),
    CONSTRAINT ck_staff_invitations_token_hash_length
        CHECK (length(token_hash) = 64),
    CONSTRAINT ck_staff_invitations_expiry
        CHECK (expires_at > created_at)
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_staff_invitations_token_hash
    ON auth.staff_invitations (token_hash);

CREATE UNIQUE INDEX IF NOT EXISTS ux_staff_invitations_outstanding_user_purpose
    ON auth.staff_invitations (user_id, purpose)
    WHERE used_at IS NULL
      AND revoked_at IS NULL;

CREATE INDEX IF NOT EXISTS ix_staff_invitations_user_purpose_expiry
    ON auth.staff_invitations (user_id, purpose, expires_at)
    WHERE used_at IS NULL
      AND revoked_at IS NULL;

INSERT INTO auth.permissions (id, code, name, description, created_at, updated_at)
VALUES
    (uuid_generate_v7(), 'staff.view', 'View Staff', 'View Staff assigned to owned branches.', now(), now()),
    (uuid_generate_v7(), 'staff.create', 'Create Staff', 'Create Staff accounts for owned branches.', now(), now()),
    (uuid_generate_v7(), 'staff.update', 'Update Staff', 'Update permitted Staff profile and employment fields.', now(), now()),
    (uuid_generate_v7(), 'staff.activate', 'Activate Staff', 'Reactivate a previously activated Staff account.', now(), now()),
    (uuid_generate_v7(), 'staff.deactivate', 'Deactivate Staff', 'Deactivate Staff accounts and revoke active sessions.', now(), now()),
    (uuid_generate_v7(), 'staff.assign_branch', 'Assign Staff Branch', 'Assign Staff to owned branches.', now(), now()),
    (uuid_generate_v7(), 'staff.remove_branch', 'Remove Staff Branch', 'Remove Staff assignments from owned branches.', now(), now()),
    (uuid_generate_v7(), 'staff.reset_password', 'Reset Staff Password', 'Send a password reset invitation to Staff.', now(), now())
ON CONFLICT (code) DO NOTHING;

INSERT INTO auth.role_permissions (role_id, permission_id)
SELECT role_row.id, permission_row.id
FROM auth.roles role_row
CROSS JOIN auth.permissions permission_row
WHERE role_row.code = 'BRANCH_ADMIN'
  AND role_row.is_active
  AND role_row.deleted_at IS NULL
  AND permission_row.code IN (
      'staff.view',
      'staff.create',
      'staff.update',
      'staff.activate',
      'staff.deactivate',
      'staff.assign_branch',
      'staff.remove_branch',
      'staff.reset_password'
  )
ON CONFLICT (role_id, permission_id) DO NOTHING;

COMMIT;
