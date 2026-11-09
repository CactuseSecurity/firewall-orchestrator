-- Fix duplicate flow foreign key constraints introduced by mixed fresh-install and upgrade paths.

DO $$
DECLARE
    rec record;
BEGIN
    -- public.object.flow_nwobj_id -> flow.nwobject
    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'object'
          AND column_name = 'flow_nwobj_id'
    ) AND to_regclass('flow.nwobject') IS NOT NULL THEN
        FOR rec IN
            SELECT c.conname
            FROM pg_constraint c
            JOIN pg_attribute a ON a.attrelid = c.conrelid AND a.attnum = ANY (c.conkey)
            WHERE c.contype = 'f'
              AND c.conrelid = 'public.object'::regclass
              AND c.confrelid = 'flow.nwobject'::regclass
              AND a.attname = 'flow_nwobj_id'
        LOOP
            EXECUTE format('ALTER TABLE public.object DROP CONSTRAINT IF EXISTS %I', rec.conname);
        END LOOP;

        ALTER TABLE public.object
            ADD CONSTRAINT flow_nwobj_id_foreign_key
            FOREIGN KEY (flow_nwobj_id) REFERENCES flow.nwobject(nwobj_id)
            ON UPDATE RESTRICT ON DELETE SET NULL;
    END IF;

    -- public.object.flow_nwgrp_id -> flow.nwgroup
    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'object'
          AND column_name = 'flow_nwgrp_id'
    ) AND to_regclass('flow.nwgroup') IS NOT NULL THEN
        FOR rec IN
            SELECT c.conname
            FROM pg_constraint c
            JOIN pg_attribute a ON a.attrelid = c.conrelid AND a.attnum = ANY (c.conkey)
            WHERE c.contype = 'f'
              AND c.conrelid = 'public.object'::regclass
              AND c.confrelid = 'flow.nwgroup'::regclass
              AND a.attname = 'flow_nwgrp_id'
        LOOP
            EXECUTE format('ALTER TABLE public.object DROP CONSTRAINT IF EXISTS %I', rec.conname);
        END LOOP;

        ALTER TABLE public.object
            ADD CONSTRAINT flow_nwgrp_id_foreign_key
            FOREIGN KEY (flow_nwgrp_id) REFERENCES flow.nwgroup(nwgrp_id)
            ON UPDATE RESTRICT ON DELETE SET NULL;
    END IF;

    -- public.service.flow_svcobj_id -> flow.svcobject
    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'service'
          AND column_name = 'flow_svcobj_id'
    ) AND to_regclass('flow.svcobject') IS NOT NULL THEN
        FOR rec IN
            SELECT c.conname
            FROM pg_constraint c
            JOIN pg_attribute a ON a.attrelid = c.conrelid AND a.attnum = ANY (c.conkey)
            WHERE c.contype = 'f'
              AND c.conrelid = 'public.service'::regclass
              AND c.confrelid = 'flow.svcobject'::regclass
              AND a.attname = 'flow_svcobj_id'
        LOOP
            EXECUTE format('ALTER TABLE public.service DROP CONSTRAINT IF EXISTS %I', rec.conname);
        END LOOP;

        ALTER TABLE public.service
            ADD CONSTRAINT flow_svcobj_id_foreign_key
            FOREIGN KEY (flow_svcobj_id) REFERENCES flow.svcobject(svcobj_id)
            ON UPDATE RESTRICT ON DELETE SET NULL;
    END IF;

    -- public.service.flow_svcgrp_id -> flow.svcgroup
    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'service'
          AND column_name = 'flow_svcgrp_id'
    ) AND to_regclass('flow.svcgroup') IS NOT NULL THEN
        FOR rec IN
            SELECT c.conname
            FROM pg_constraint c
            JOIN pg_attribute a ON a.attrelid = c.conrelid AND a.attnum = ANY (c.conkey)
            WHERE c.contype = 'f'
              AND c.conrelid = 'public.service'::regclass
              AND c.confrelid = 'flow.svcgroup'::regclass
              AND a.attname = 'flow_svcgrp_id'
        LOOP
            EXECUTE format('ALTER TABLE public.service DROP CONSTRAINT IF EXISTS %I', rec.conname);
        END LOOP;

        ALTER TABLE public.service
            ADD CONSTRAINT flow_svcgrp_id_foreign_key
            FOREIGN KEY (flow_svcgrp_id) REFERENCES flow.svcgroup(svcgrp_id)
            ON UPDATE RESTRICT ON DELETE SET NULL;
    END IF;

    -- public.time_object.flow_timeobj_id -> flow.timeobject
    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'time_object'
          AND column_name = 'flow_timeobj_id'
    ) AND to_regclass('flow.timeobject') IS NOT NULL THEN
        FOR rec IN
            SELECT c.conname
            FROM pg_constraint c
            JOIN pg_attribute a ON a.attrelid = c.conrelid AND a.attnum = ANY (c.conkey)
            WHERE c.contype = 'f'
              AND c.conrelid = 'public.time_object'::regclass
              AND c.confrelid = 'flow.timeobject'::regclass
              AND a.attname = 'flow_timeobj_id'
        LOOP
            EXECUTE format('ALTER TABLE public.time_object DROP CONSTRAINT IF EXISTS %I', rec.conname);
        END LOOP;

        ALTER TABLE public.time_object
            ADD CONSTRAINT flow_timeobj_id_foreign_key
            FOREIGN KEY (flow_timeobj_id) REFERENCES flow.timeobject(timeobj_id)
            ON UPDATE RESTRICT ON DELETE SET NULL;
    END IF;
END $$;
