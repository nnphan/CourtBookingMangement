BEGIN;

INSERT INTO auth.permissions (id, code, name, description, created_at, updated_at)
VALUES (
    gen_random_uuid(),
    'file.upload',
    'Upload files',
    'Upload image files to configured file storage.',
    NOW(),
    NOW()
)
ON CONFLICT (code) DO NOTHING;

COMMIT;