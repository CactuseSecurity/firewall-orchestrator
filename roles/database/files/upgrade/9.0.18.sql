DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM information_schema.tables
        WHERE table_schema = 'compliance'
          AND table_name = 'criterion_condition'
    ) AND NOT EXISTS (
        SELECT 1
        FROM information_schema.tables
        WHERE table_schema = 'compliance'
          AND table_name = 'condition'
    ) THEN
        ALTER TABLE compliance.criterion_condition RENAME TO condition;
    END IF;
END $$;

CREATE TABLE IF NOT EXISTS compliance.condition
(
    id BIGSERIAL PRIMARY KEY,
    criterion_id INT NOT NULL,
    group_order INT NOT NULL DEFAULT 1,
    position INT NOT NULL DEFAULT 1,
    field TEXT NOT NULL,
    operator TEXT NOT NULL,
    value_string TEXT,
    value_int INT,
    value_int_end INT,
    value_ref BIGINT,
    removed TIMESTAMP WITH TIME ZONE,
    created TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    CONSTRAINT compliance_condition_group_order_check CHECK (group_order >= 1),
    CONSTRAINT compliance_condition_position_check CHECK (position >= 1),
    CONSTRAINT compliance_condition_value_range_start_check CHECK (value_int_end IS NULL OR value_int IS NOT NULL),
    CONSTRAINT compliance_condition_value_range_order_check CHECK (value_int IS NULL OR value_int_end IS NULL OR value_int <= value_int_end),
    CONSTRAINT compliance_condition_value_presence_check CHECK (
        value_string IS NOT NULL
        OR value_int IS NOT NULL
        OR value_ref IS NOT NULL
    ),
    CONSTRAINT compliance_criterion_condition_foreign_key
    FOREIGN KEY (criterion_id) REFERENCES compliance.criterion(id)
    ON UPDATE RESTRICT ON DELETE CASCADE
);

ALTER SEQUENCE IF EXISTS compliance.criterion_condition_id_seq RENAME TO condition_id_seq;

DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'criterion_condition_pkey'
          AND conrelid = 'compliance.condition'::regclass
    ) THEN
        ALTER TABLE compliance.condition RENAME CONSTRAINT criterion_condition_pkey TO condition_pkey;
    END IF;

    IF EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'compliance_criterion_criterion_condition_foreign_key'
          AND conrelid = 'compliance.condition'::regclass
    ) THEN
        ALTER TABLE compliance.condition RENAME CONSTRAINT compliance_criterion_criterion_condition_foreign_key TO compliance_criterion_condition_foreign_key;
    END IF;

    IF EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'compliance_criterion_condition_group_order_check'
          AND conrelid = 'compliance.condition'::regclass
    ) THEN
        ALTER TABLE compliance.condition RENAME CONSTRAINT compliance_criterion_condition_group_order_check TO compliance_condition_group_order_check;
    END IF;

    IF EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'compliance_criterion_condition_position_check'
          AND conrelid = 'compliance.condition'::regclass
    ) THEN
        ALTER TABLE compliance.condition RENAME CONSTRAINT compliance_criterion_condition_position_check TO compliance_condition_position_check;
    END IF;

    IF EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'compliance_criterion_condition_value_range_start_check'
          AND conrelid = 'compliance.condition'::regclass
    ) THEN
        ALTER TABLE compliance.condition RENAME CONSTRAINT compliance_criterion_condition_value_range_start_check TO compliance_condition_value_range_start_check;
    END IF;

    IF EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'compliance_criterion_condition_value_range_order_check'
          AND conrelid = 'compliance.condition'::regclass
    ) THEN
        ALTER TABLE compliance.condition RENAME CONSTRAINT compliance_criterion_condition_value_range_order_check TO compliance_condition_value_range_order_check;
    END IF;

    IF EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'compliance_criterion_condition_value_presence_check'
          AND conrelid = 'compliance.condition'::regclass
    ) THEN
        ALTER TABLE compliance.condition RENAME CONSTRAINT compliance_criterion_condition_value_presence_check TO compliance_condition_value_presence_check;
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'compliance_criterion_condition_foreign_key'
          AND conrelid = 'compliance.condition'::regclass
    ) THEN
        ALTER TABLE compliance.condition
        ADD CONSTRAINT compliance_criterion_condition_foreign_key
        FOREIGN KEY (criterion_id) REFERENCES compliance.criterion(id)
        ON UPDATE RESTRICT ON DELETE CASCADE;
    END IF;
END $$;

ALTER INDEX IF EXISTS compliance.idx_fkey_criterion_condition_criterion_id RENAME TO idx_fkey_condition_criterion_id;
ALTER INDEX IF EXISTS compliance.idx_criterion_condition_order RENAME TO idx_condition_order;

CREATE INDEX IF NOT EXISTS idx_fkey_condition_criterion_id ON compliance.condition USING HASH (criterion_id);
CREATE INDEX IF NOT EXISTS idx_condition_order ON compliance.condition (criterion_id, group_order, position);
