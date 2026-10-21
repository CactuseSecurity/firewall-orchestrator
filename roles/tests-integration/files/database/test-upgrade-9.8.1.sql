-- Test the rule-position schema change and repeatability of upgrade/9.8.1.sql.
-- The test task inserts the upgrade file at both run-upgrade markers; the transaction is rolled back.
\set ON_ERROR_STOP 1
BEGIN;
ALTER TABLE request.impltask DROP COLUMN IF EXISTS rule_position;

CREATE TEMP TABLE rule_position_probe (impltask_id bigint) ON COMMIT DROP;
WITH inserted AS (
    INSERT INTO request.impltask (state_id, task_type, implementation_action)
    VALUES (0, 'upgrade-rule-position-probe', 'create')
    RETURNING id
)
INSERT INTO rule_position_probe SELECT id FROM inserted;

-- @run-upgrade-9.8.1

DO $$
DECLARE
    i_impltask_id bigint;
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'request' AND table_name = 'impltask' AND column_name = 'rule_position'
            AND data_type = 'jsonb' AND is_nullable = 'YES'
    ) THEN
        RAISE EXCEPTION 'upgrade 9.8.1: nullable request.impltask.rule_position jsonb column is missing';
    END IF;
    SELECT impltask_id INTO i_impltask_id FROM rule_position_probe;
    IF (SELECT rule_position FROM request.impltask WHERE id = i_impltask_id) IS NOT NULL THEN
        RAISE EXCEPTION 'upgrade 9.8.1: existing implementation task did not receive a NULL rule position';
    END IF;
    UPDATE request.impltask
    SET rule_position = '{"type":"upgrade.test-position.v1","probe":true}'::jsonb
    WHERE id = i_impltask_id;
END $$;

-- @run-upgrade-9.8.1

DO $$
DECLARE
    i_impltask_id bigint;
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'request' AND table_name = 'impltask' AND column_name = 'rule_position'
            AND data_type = 'jsonb' AND is_nullable = 'YES'
    ) THEN
        RAISE EXCEPTION 'upgrade 9.8.1: rerunning the migration changed the rule-position column';
    END IF;
    SELECT impltask_id INTO i_impltask_id FROM rule_position_probe;
    IF (SELECT rule_position FROM request.impltask WHERE id = i_impltask_id)
        IS DISTINCT FROM '{"type":"upgrade.test-position.v1","probe":true}'::jsonb THEN
        RAISE EXCEPTION 'upgrade 9.8.1: rerunning the migration changed an existing rule position';
    END IF;
END $$;
ROLLBACK;
\echo '9.8.1 rule-position migration and idempotency checks passed'
