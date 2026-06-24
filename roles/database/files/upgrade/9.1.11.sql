DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conrelid = 'public.rule_owner'::regclass
          AND conname = 'rule_owner_pkey'
          AND contype = 'p'
    )
    AND NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conrelid = 'public.rule_owner'::regclass
          AND conname = 'pk_rule_owner'
    ) THEN
        ALTER TABLE public.rule_owner
        RENAME CONSTRAINT rule_owner_pkey TO pk_rule_owner;
    END IF;
END $$;