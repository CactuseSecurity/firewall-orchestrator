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

-- rename primary key
DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conrelid = 'network_zone.zone'::regclass
        AND conname = 'network_zone_pkey'
    )
    AND NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conrelid = 'network_zone.zone'::regclass
        AND conname = 'zone_pkey'
    )
    THEN ALTER TABLE network_zone.zone RENAME CONSTRAINT network_zone_pkey TO zone_pkey;
    END IF;
END $$;

-- rename sequence
DO $$
BEGIN
    IF to_regclass('network_zone.network_zone_id_seq') IS NOT NULL
    AND to_regclass('network_zone.zone_id_seq') IS NULL
    THEN ALTER SEQUENCE network_zone.network_zone_id_seq RENAME TO zone_id_seq;
    END IF;
END $$;

GRANT USAGE ON SCHEMA network_zone TO fwo_ro;
GRANT SELECT ON ALL TABLES IN SCHEMA network_zone TO fwo_ro;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA network_zone TO fwo_ro;
ALTER DEFAULT PRIVILEGES IN SCHEMA network_zone GRANT SELECT ON TABLES TO fwo_ro;
ALTER DEFAULT PRIVILEGES IN SCHEMA network_zone GRANT USAGE, SELECT ON SEQUENCES TO fwo_ro;

-- path analysis algorithm
CREATE TABLE IF NOT EXISTS "path_analysis_algorithm"
(
	"id" BIGSERIAL PRIMARY KEY,
	"name" varchar NOT NULL UNIQUE
);

INSERT INTO path_analysis_algorithm (name)
VALUES ('None')
ON CONFLICT (name) DO NOTHING;

INSERT INTO config (config_key, config_value, config_user)
VALUES ('pathAnalysisAlgorithm', 'None', 0)
ON CONFLICT (config_key, config_user) DO NOTHING;
