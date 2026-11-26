BEGIN;

INSERT INTO auth.role_permissions (role_id, permission_id)
SELECT role_row.id, permission_row.id
FROM auth.roles role_row
CROSS JOIN auth.permissions permission_row
WHERE role_row.code = 'BRANCH_OWNER'
  AND role_row.is_active
  AND role_row.deleted_at IS NULL
  AND permission_row.code IN ('staff.view', 'staff.create')
ON CONFLICT (role_id, permission_id) DO NOTHING;

COMMIT;
