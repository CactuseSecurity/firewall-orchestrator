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

    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conrelid = 'public.rule_owner'::regclass
          AND contype = 'p'
    )
    AND NOT EXISTS (
        SELECT 1
        FROM public.rule_owner
        WHERE rule_id IS NULL
           OR owner_id IS NULL
           OR created IS NULL
    ) THEN
        ALTER TABLE public.rule_owner
        ADD CONSTRAINT pk_rule_owner
        PRIMARY KEY (rule_id, owner_id, created);
    END IF;
END $$;