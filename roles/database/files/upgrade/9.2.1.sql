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

-- make the firewall schema resolvable for unqualified references in functions,
-- triggers and views. current_database() avoids templating the database name.
DO $$
BEGIN
    EXECUTE format('ALTER DATABASE %I SET search_path = %s', current_database(), '"$user", public, firewall');
END $$;
