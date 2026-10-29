ALTER TABLE request.impltask
    ADD COLUMN IF NOT EXISTS rule_position jsonb;
