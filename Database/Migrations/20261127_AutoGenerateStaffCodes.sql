BEGIN;

CREATE SEQUENCE IF NOT EXISTS auth.staff_code_seq AS bigint START WITH 1;

SELECT setval(
    'auth.staff_code_seq',
    GREATEST(sequence_state.last_value, existing.max_code + 1),
    sequence_state.is_called AND sequence_state.last_value > existing.max_code
)
FROM auth.staff_code_seq sequence_state
CROSS JOIN (
    SELECT COALESCE(MAX(SUBSTRING(staff_code FROM 3)::bigint), 0) AS max_code
    FROM auth.branch_user_roles
    WHERE staff_code ~* '^NV[0-9]{1,18}$'
) existing;

COMMIT;
