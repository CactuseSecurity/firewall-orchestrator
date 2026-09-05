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
    -- Names the subsystem that wrote the row. It is the discriminator for object_type
    -- and the basis of the read permissions of the modelling roles, so it is set by the
    -- API and never derived from imported data.
    module VARCHAR NOT NULL DEFAULT 'modelling',
    change_type INTEGER,
    -- Holds two disjoint enums, selected by module:
    -- FWO.Data.Modelling.ModellingTypes.ModObjectType (1-31) for module = 'modelling',
    -- FWO.Data.ChangeHistoryObjectType (100 and above) for module = 'workflow'.
    object_type INTEGER,
    object_id BIGINT,
    change_text TEXT,
    -- Free text supplied by the client. changer_id is set by the API from the
    -- authenticated session and is the trustworthy identity of the two.
    changer VARCHAR,
    changer_id INTEGER,
    change_time TIMESTAMP DEFAULT NOW(),
    -- Provenance within the module, e.g. manual, adjustAppServerNames or an import source
    -- name configured by the customer. Never used to tell modules apart, see module.
    change_source VARCHAR NOT NULL DEFAULT 'manual',
    -- FWO.Data.Workflow.WorkflowPhases, null for modelling changes. Note that request = 0.
    workflow_phase INTEGER,
    old_data JSONB,
    new_data JSONB,
    audit_proof_critical BOOLEAN NOT NULL DEFAULT FALSE
);

-- Every entry that exists before this upgrade is modelling history, so the column default
-- migrates them without a separate backfill.
ALTER TABLE public.change_history
ADD COLUMN IF NOT EXISTS module VARCHAR NOT NULL DEFAULT 'modelling';

ALTER TABLE public.change_history
ADD COLUMN IF NOT EXISTS ticket_id BIGINT;

ALTER TABLE public.change_history
ADD COLUMN IF NOT EXISTS changer_id INTEGER;

ALTER TABLE public.change_history
ADD COLUMN IF NOT EXISTS workflow_phase INTEGER;

ALTER TABLE public.change_history
ADD COLUMN IF NOT EXISTS old_data JSONB;

ALTER TABLE public.change_history
ADD COLUMN IF NOT EXISTS new_data JSONB;

ALTER TABLE public.change_history
ADD COLUMN IF NOT EXISTS audit_proof_critical BOOLEAN NOT NULL DEFAULT FALSE;

ALTER TABLE public.change_history
DROP CONSTRAINT IF EXISTS change_history_module_check;

ALTER TABLE public.change_history
ADD CONSTRAINT change_history_module_check CHECK (module IN ('modelling', 'workflow'));

UPDATE public.change_history SET change_source = 'manual' WHERE change_source IS NULL;

ALTER TABLE public.change_history
ALTER COLUMN change_source SET DEFAULT 'manual';

ALTER TABLE public.change_history
ALTER COLUMN change_source SET NOT NULL;

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

-- The table is insert heavy and read rarely, so the index set is kept minimal and the two
-- per-object indices are partial: a workflow row has no app_id and a modelling row has no
-- ticket_id, so each row maintains only the indices that apply to it. id is part of the sort
-- key because change_time is not unique and paging by it alone is unstable.
DROP INDEX IF EXISTS public.idx_change_history_app_id;

DROP INDEX IF EXISTS public.idx_change_history_ticket_id;

DROP INDEX IF EXISTS public.idx_change_history_change_time;

DROP INDEX IF EXISTS public.idx_modelling_change_history01;

CREATE INDEX IF NOT EXISTS idx_change_history_module_time ON public.change_history (module, change_time DESC, id DESC);

CREATE INDEX IF NOT EXISTS idx_change_history_app_time ON public.change_history (app_id, change_time DESC) WHERE app_id IS NOT NULL;

CREATE INDEX IF NOT EXISTS idx_change_history_ticket_time ON public.change_history (ticket_id, change_time DESC) WHERE ticket_id IS NOT NULL;

CREATE INDEX IF NOT EXISTS idx_change_history_audit_proof ON public.change_history (change_time DESC) WHERE audit_proof_critical;

GRANT SELECT ON public.change_history TO fwo_ro;

GRANT SELECT ON SEQUENCE public.change_history_id_seq TO fwo_ro;
