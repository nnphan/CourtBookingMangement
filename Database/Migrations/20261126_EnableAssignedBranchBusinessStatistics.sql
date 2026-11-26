BEGIN;

INSERT INTO auth.role_permissions (role_id, permission_id)
SELECT role_row.id, permission_row.id
FROM auth.roles role_row
CROSS JOIN auth.permissions permission_row
WHERE role_row.code = 'BRANCH_MANAGER'
  AND role_row.is_active
  AND role_row.deleted_at IS NULL
  AND permission_row.code = 'sales.report.view'
ON CONFLICT (role_id, permission_id) DO NOTHING;

-- Retire unused legacy roles while preserving referenced records and the staff invitation workflow.
UPDATE auth.roles role_row
SET is_active = false, updated_at = now()
WHERE role_row.is_active AND role_row.deleted_at IS NULL
  AND role_row.code NOT IN ('ADMIN', 'CUSTOMER', 'BRANCH_OWNER', 'BRANCH_MANAGER', 'STAFF')
  AND NOT EXISTS (
      SELECT 1 FROM auth.user_roles assignment
      JOIN auth.users account ON account.id = assignment.user_id
      WHERE assignment.role_id = role_row.id AND account.is_active AND account.deleted_at IS NULL
  )
  AND NOT EXISTS (
      SELECT 1 FROM auth.branch_user_roles assignment
      WHERE assignment.role_id = role_row.id AND assignment.is_active AND assignment.removed_at IS NULL
  );

COMMIT;
