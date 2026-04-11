ALTER TABLE IF EXISTS public.owner
    ADD COLUMN IF NOT EXISTS additional_info JSONB;
