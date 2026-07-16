-- issue #4793: move the firewall configuration tables from the public schema
-- to a new firewall schema and rename nine of them.
--
-- ALTER TABLE ... SET SCHEMA / RENAME keep indexes, constraints, owned
-- sequences and triggers attached, and existing foreign keys stay valid
-- because PostgreSQL tracks them by OID, not by name. Functions and views
-- referencing the old names are recreated by the idempotent step that runs
-- after the upgrade files.

CREATE SCHEMA IF NOT EXISTS firewall;

-- move and rename the tables, guarded so the upgrade can be re-run safely
DO $$
DECLARE
    r RECORD;
BEGIN
    FOR r IN
        SELECT * FROM (VALUES
            ('rule',                    'rule'),
            ('rule_metadata',           'rule_metadata'),
            ('parent_rule_type',        'parent_rule_type'),
            ('object',                  'nw_object'),
            ('objgrp',                  'nw_object_group'),
            ('service',                 'nw_service'),
            ('svcgrp',                  'nw_service_group'),
            ('usr',                     'nw_user'),
            ('usergrp',                 'nw_user_group'),
            ('zone',                    'zone'),
            ('rule_svc_resolved',       'rule_nw_service_resolved'),
            ('rule_nwobj_resolved',     'rule_nw_object_resolved'),
            ('rule_user_resolved',      'rule_nw_user_resolved'),
            ('rule_from',               'rule_from'),
            ('rule_service',            'rule_service'),
            ('rule_to',                 'rule_to'),
            ('rulebase',                'rulebase'),
            ('rulebase_link',           'rulebase_link'),
            ('rule_enforced_on_gateway','rule_enforced_on_gateway'),
            ('rule_from_zone',          'rule_from_zone'),
            ('rule_to_zone',            'rule_to_zone'),
            ('rule_time',               'rule_time')
        ) AS t(old_name, new_name)
    LOOP
        IF EXISTS (SELECT 1 FROM pg_tables WHERE schemaname = 'public' AND tablename = r.old_name) THEN
            EXECUTE format('ALTER TABLE public.%I SET SCHEMA firewall', r.old_name);
            IF r.old_name <> r.new_name THEN
                EXECUTE format('ALTER TABLE firewall.%I RENAME TO %I', r.old_name, r.new_name);
            END IF;
        END IF;
    END LOOP;
END $$;

-- align auto-generated names of constraints and owned sequences with what a fresh
-- install generates for the renamed tables: RENAME TO does not touch dependent
-- names, so e.g. the primary key of firewall.nw_object would keep its historical
-- name object_pkey, while a fresh install creates nw_object_pkey. Only names
-- following the auto-generation patterns (<table>_pkey, <table>_<cols>_fkey,
-- <table>_<col>_check, <table>_<col>_seq) are touched; explicitly named
-- constraints (obj_altkey, object_obj_ip_is_host, ...) are identical on both
-- install paths and stay as they are. Guarded so the upgrade can be re-run and
-- so installations with historically different auto-names are left alone.
DO $$
DECLARE
    pair RECORD;
    con RECORD;
    seq RECORD;
    target_name TEXT;
BEGIN
    FOR pair IN
        SELECT * FROM (VALUES
            ('object',              'nw_object'),
            ('objgrp',              'nw_object_group'),
            ('service',             'nw_service'),
            ('svcgrp',              'nw_service_group'),
            ('usr',                 'nw_user'),
            ('usergrp',             'nw_user_group'),
            ('rule_svc_resolved',   'rule_nw_service_resolved'),
            ('rule_nwobj_resolved', 'rule_nw_object_resolved'),
            ('rule_user_resolved',  'rule_nw_user_resolved')
        ) AS t(old_name, new_name)
    LOOP
        FOR con IN
            SELECT c.conname
            FROM pg_constraint c
            JOIN pg_class cl ON cl.oid = c.conrelid
            JOIN pg_namespace n ON n.oid = cl.relnamespace
            WHERE n.nspname = 'firewall'
              AND cl.relname = pair.new_name
              AND (c.conname = pair.old_name || '_pkey'
                   OR (c.conname LIKE pair.old_name || '\_%'
                       AND (c.conname LIKE '%\_fkey%' OR c.conname LIKE '%\_check%')))
        LOOP
            target_name := pair.new_name || substr(con.conname, length(pair.old_name) + 1);
            IF NOT EXISTS (
                SELECT 1 FROM pg_constraint c2
                JOIN pg_class cl2 ON cl2.oid = c2.conrelid
                JOIN pg_namespace n2 ON n2.oid = cl2.relnamespace
                WHERE n2.nspname = 'firewall' AND cl2.relname = pair.new_name AND c2.conname = target_name
            ) THEN
                EXECUTE format('ALTER TABLE firewall.%I RENAME CONSTRAINT %I TO %I',
                               pair.new_name, con.conname, target_name);
            END IF;
        END LOOP;

        FOR seq IN
            SELECT cl.relname
            FROM pg_class cl
            JOIN pg_namespace n ON n.oid = cl.relnamespace
            WHERE n.nspname = 'firewall' AND cl.relkind = 'S'
              AND cl.relname LIKE pair.old_name || '\_%\_seq'
        LOOP
            target_name := pair.new_name || substr(seq.relname, length(pair.old_name) + 1);
            IF NOT EXISTS (
                SELECT 1 FROM pg_class cl2
                JOIN pg_namespace n2 ON n2.oid = cl2.relnamespace
                WHERE n2.nspname = 'firewall' AND cl2.relname = target_name
            ) THEN
                EXECUTE format('ALTER SEQUENCE firewall.%I RENAME TO %I', seq.relname, target_name);
            END IF;
        END LOOP;
    END LOOP;
END $$;

-- make the firewall schema resolvable for unqualified references in functions,
-- triggers and views. current_database() avoids templating the database name.
DO $$
BEGIN
    EXECUTE format('ALTER DATABASE %I SET search_path = %s', current_database(), '"$user", public, firewall');
END $$;

-- give the read-only user access to the new schema (same pattern as 8.8.8.sql);
-- the moved tables keep their table-level grants, but schema USAGE and default
-- privileges for future tables have to be granted explicitly.
DO $$
BEGIN
    IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'fwo_ro') THEN
        CREATE ROLE fwo_ro WITH LOGIN NOSUPERUSER INHERIT NOCREATEDB NOCREATEROLE;
    END IF;
END
$$;

GRANT USAGE ON SCHEMA firewall TO fwo_ro;
GRANT SELECT ON ALL TABLES IN SCHEMA firewall TO fwo_ro;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA firewall TO fwo_ro;
ALTER DEFAULT PRIVILEGES IN SCHEMA firewall GRANT SELECT ON TABLES TO fwo_ro;
ALTER DEFAULT PRIVILEGES IN SCHEMA firewall GRANT USAGE, SELECT ON SEQUENCES TO fwo_ro;
