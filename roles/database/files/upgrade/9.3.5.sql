-- issue #561: create new db schema network_zone 

create schema if not exists network_zone;

-- move and rename the tables, guarded so the upgrade can be re-run safely
DO $$
DECLARE
    r RECORD;
BEGIN
    FOR r IN
        SELECT * FROM (VALUES
            ('network_zone',    'zone'),
            ('ip_range',        'ip_range')
        ) AS t(old_name, new_name)
    LOOP
        IF EXISTS (SELECT 1 FROM pg_tables WHERE schemaname = 'compliance' AND tablename = r.old_name) THEN
            EXECUTE format('ALTER TABLE compliance.%I SET SCHEMA network_zone', r.old_name);
            IF r.old_name <> r.new_name THEN
                EXECUTE format('ALTER TABLE network_zone.%I RENAME TO %I', r.old_name, r.new_name);
            END IF;
        END IF;
    END LOOP;
END $$;

-- rename foreign keys
DO $$
DECLARE
    r RECORD;
BEGIN
    FOR r IN
        SELECT * FROM (VALUES
            ('compliance_ip_range_network_zone_foreign_key',    'network_zone_ip_range_zone_foreign_key', 'network_zone', 'ip_range'),
            ('compliance_super_zone_foreign_key',    'network_zone_super_zone_foreign_key', 'network_zone', 'zone'),
            ('compliance_from_network_zone_communication_foreign_key',    'network_zone_from_zone_communication_foreign_key', 'compliance', 'network_zone_communication'),
            ('compliance_to_network_zone_communication_foreign_key',    'network_zone_to_zone_communication_foreign_key', 'compliance', 'network_zone_communication')
        ) AS t(old_name, new_name, schema_name, table_name)
    LOOP
        IF EXISTS (
            SELECT 1
            FROM pg_constraint
            WHERE conrelid = to_regclass(format('%I.%I', r.schema_name, r.table_name))
            AND conname = r.old_name
        )
        AND NOT EXISTS (
            SELECT 1
            FROM pg_constraint
            WHERE conrelid = to_regclass(format('%I.%I', r.schema_name, r.table_name))
            AND conname = r.new_name
        ) THEN
            EXECUTE format('ALTER TABLE %I.%I RENAME CONSTRAINT %I TO %I',
                r.schema_name,
                r.table_name,
                r.old_name,
                r.new_name
            );
        END IF;
    END LOOP;
END $$;
