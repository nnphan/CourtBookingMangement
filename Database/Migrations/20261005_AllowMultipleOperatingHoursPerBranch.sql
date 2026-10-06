BEGIN;

DROP INDEX IF EXISTS core.ux_operating_hours;
CREATE UNIQUE INDEX IF NOT EXISTS ux_branches_owner_name_active
	ON core.branches (owner_id, name)
	WHERE deleted_at IS NULL;

COMMIT;
