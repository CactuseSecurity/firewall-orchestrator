CREATE TABLE IF NOT EXISTS compliance.criterion_condition
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
    CONSTRAINT compliance_criterion_condition_group_order_check CHECK (group_order >= 1),
    CONSTRAINT compliance_criterion_condition_position_check CHECK (position >= 1),
    CONSTRAINT compliance_criterion_condition_value_range_start_check CHECK (value_int_end IS NULL OR value_int IS NOT NULL),
    CONSTRAINT compliance_criterion_condition_value_range_order_check CHECK (value_int IS NULL OR value_int_end IS NULL OR value_int <= value_int_end),
    CONSTRAINT compliance_criterion_condition_value_presence_check CHECK (
        value_string IS NOT NULL
        OR value_int IS NOT NULL
        OR value_ref IS NOT NULL
    )
);

ALTER TABLE compliance.criterion_condition
DROP CONSTRAINT IF EXISTS compliance_criterion_criterion_condition_foreign_key;
ALTER TABLE compliance.criterion_condition
ADD CONSTRAINT compliance_criterion_criterion_condition_foreign_key
FOREIGN KEY (criterion_id) REFERENCES compliance.criterion(id)
ON UPDATE RESTRICT ON DELETE CASCADE;

CREATE INDEX IF NOT EXISTS idx_fkey_criterion_condition_criterion_id ON compliance.criterion_condition USING HASH (criterion_id);
CREATE INDEX IF NOT EXISTS idx_criterion_condition_order ON compliance.criterion_condition (criterion_id, group_order, position);
