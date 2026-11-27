BEGIN;

CREATE TABLE IF NOT EXISTS sales.operating_expenses (
    id uuid PRIMARY KEY,
    branch_id uuid NOT NULL REFERENCES core.branches(id),
    expense_date date NOT NULL,
    category varchar(24) NOT NULL CHECK (category IN (
        'electricity', 'water', 'payroll', 'rent', 'maintenance', 'cleaning',
        'internet_software', 'marketing', 'supplies', 'other'
    )),
    title varchar(200) NOT NULL CHECK (length(btrim(title)) > 0),
    amount numeric(14, 2) NOT NULL CHECK (amount > 0),
    notes varchar(1000),
    created_by uuid NOT NULL REFERENCES auth.users(id),
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_operating_expenses_branch_date
    ON sales.operating_expenses (branch_id, expense_date DESC);

COMMIT;
