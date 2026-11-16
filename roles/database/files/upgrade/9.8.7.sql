ALTER TABLE request.impltask
    ADD COLUMN IF NOT EXISTS rule_position jsonb;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conrelid = 'request.impltask'::regclass
            AND conname = 'request_impltask_rule_position_object'
    ) THEN
        ALTER TABLE request.impltask
            ADD CONSTRAINT request_impltask_rule_position_object
            CHECK (rule_position IS NULL OR jsonb_typeof(rule_position) = 'object');
    END IF;
END $$;
