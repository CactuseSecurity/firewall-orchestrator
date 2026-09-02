-- Centralize modelling and workflow history without losing existing entries.
DO $$
BEGIN
    IF to_regclass('public.change_history') IS NULL
       AND to_regclass('modelling.change_history') IS NOT NULL THEN
        ALTER TABLE modelling.change_history SET SCHEMA public;
    END IF;
END
$$;

CREATE TABLE IF NOT EXISTS public.change_history (
    id BIGSERIAL PRIMARY KEY,
    app_id INTEGER,
    ticket_id BIGINT,
    change_type INTEGER,
    object_type INTEGER,
    object_id BIGINT,
    change_text TEXT,
    changer VARCHAR,
    change_time TIMESTAMP DEFAULT NOW(),
    change_source VARCHAR DEFAULT 'manual',
    workflow_phase INTEGER,
    old_data JSONB,
    new_data JSONB,
    audit_prove_critical BOOLEAN NOT NULL DEFAULT FALSE
);

ALTER TABLE public.change_history
ADD COLUMN IF NOT EXISTS ticket_id BIGINT;

ALTER TABLE public.change_history
ADD COLUMN IF NOT EXISTS workflow_phase INTEGER;

ALTER TABLE public.change_history
ADD COLUMN IF NOT EXISTS old_data JSONB;

ALTER TABLE public.change_history
ADD COLUMN IF NOT EXISTS new_data JSONB;

ALTER TABLE public.change_history
ADD COLUMN IF NOT EXISTS audit_prove_critical BOOLEAN NOT NULL DEFAULT FALSE;

ALTER TABLE public.change_history
DROP CONSTRAINT IF EXISTS modelling_change_history_owner_foreign_key;

ALTER TABLE public.change_history
DROP CONSTRAINT IF EXISTS change_history_owner_foreign_key;

ALTER TABLE public.change_history
ADD CONSTRAINT change_history_owner_foreign_key FOREIGN KEY (app_id) REFERENCES public.owner (id) ON UPDATE RESTRICT ON DELETE SET NULL;

ALTER TABLE public.change_history
DROP CONSTRAINT IF EXISTS change_history_ticket_foreign_key;

ALTER TABLE public.change_history
ADD CONSTRAINT change_history_ticket_foreign_key FOREIGN KEY (ticket_id) REFERENCES request.ticket (id) ON UPDATE RESTRICT ON DELETE SET NULL;

CREATE INDEX IF NOT EXISTS idx_change_history_app_id ON public.change_history (app_id);

CREATE INDEX IF NOT EXISTS idx_change_history_ticket_id ON public.change_history (ticket_id);

CREATE INDEX IF NOT EXISTS idx_change_history_change_time ON public.change_history (change_time DESC);

GRANT SELECT ON public.change_history TO fwo_ro;

GRANT SELECT ON SEQUENCE public.change_history_id_seq TO fwo_ro;
