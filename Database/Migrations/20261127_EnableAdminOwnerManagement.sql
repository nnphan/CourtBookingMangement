BEGIN;

INSERT INTO auth.permissions (id, code, name, description, created_at, updated_at)
VALUES
    (uuid_generate_v7(), 'owner.view', 'View Branch Owners', 'View branch owner accounts and profiles.', now(), now()),
    (uuid_generate_v7(), 'owner.create', 'Create Branch Owners', 'Create branch owner profiles.', now(), now()),
    (uuid_generate_v7(), 'owner.update', 'Update Branch Owners', 'Update branch owner profiles.', now(), now()),
    (uuid_generate_v7(), 'owner.delete', 'Delete Branch Owners', 'Delete branch owner profiles.', now(), now())
ON CONFLICT (code) DO NOTHING;

INSERT INTO auth.role_permissions (role_id, permission_id)
SELECT r.id, p.id FROM auth.roles r CROSS JOIN auth.permissions p
WHERE r.code = 'ADMIN' AND r.is_active AND r.deleted_at IS NULL
  AND p.code IN ('owner.view', 'owner.create', 'owner.update', 'owner.delete')
ON CONFLICT (role_id, permission_id) DO NOTHING;

COMMIT;
