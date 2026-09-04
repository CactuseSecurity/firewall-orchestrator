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

-- renamed config keys
UPDATE config SET config_key = 'matrixAllowNestedZones'
WHERE config_key = 'complianceMatrixAllowNetworkZones';
UPDATE config SET config_key = 'sortMatrixByID'
WHERE config_key = 'complianceCheckSortMatrixByID';

-- renamed text ids
UPDATE customtxt SET id = 'autoCalcInternetZone'
WHERE id = 'complianceCheckAutoCalcInternetZone';
UPDATE customtxt SET id = 'privateAdressSpace'
WHERE id = 'complianceCheckPrivateAdressSpace';
UPDATE customtxt SET id = 'documentationSamples'
WHERE id = 'complianceCheckDocumentationSamples';
UPDATE customtxt SET id = 'treatDynamicAndDomainObjectsAsInternet'
WHERE id = 'complianceCheckTreatDynamicAndDomainObjectsAsInternet';
UPDATE customtxt SET id = 'autoCalcUndefinedInternalZone'
WHERE id = 'complianceCheckAutoCalcUndefinedInternalZone';
UPDATE customtxt SET id = 'excludeFromInternetZone'
WHERE id = 'complianceCheckExcludeFromInternetZone';
UPDATE customtxt SET id = 'loopbackLocal'
WHERE id = 'complianceCheckLoopbackLocal';
UPDATE customtxt SET id = 'multicastBroadcast'
WHERE id = 'complianceCheckMulticastBroadcast';
UPDATE customtxt SET id = 'internetSettingsDiv'
WHERE id = 'complianceCheckDiv';
UPDATE customtxt SET id = 'autoCalculatedZonesAtTheEnd'
WHERE id = 'complianceCheckAutoCalculatedZonesAtTheEnd';
UPDATE customtxt SET id = 'matrixAllowNestedZones'
WHERE id = 'complianceMatrixAllowNetworkZones';
UPDATE customtxt SET id = 'sortMatrixByID'
WHERE id = 'complianceCheckSortMatrixByID';

-- path analysis algorithm
CREATE TABLE IF NOT EXISTS "path_analysis_algorithm"
(
    "id" BIGSERIAL PRIMARY KEY,
    "name" varchar NOT NULL UNIQUE
);

INSERT INTO path_analysis_algorithm (id, name) VALUES
    (1, 'None'),
    (2, 'Network Zone Tree')
ON CONFLICT (name) DO NOTHING;

INSERT INTO config (config_key, config_value, config_user)
VALUES ('pathAnalysisAlgorithm', 1, 0)
ON CONFLICT (config_key, config_user) DO NOTHING;

GRANT SELECT ON TABLE path_analysis_algorithm TO fwo_ro;
GRANT USAGE, SELECT ON SEQUENCE path_analysis_algorithm_id_seq TO fwo_ro;

-- Network Zone Tree
ALTER TABLE network_zone.ip_range
ADD COLUMN IF NOT EXISTS id BIGSERIAL;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conrelid = 'network_zone.ip_range'::regclass
          AND conname = 'ip_range_id_pkey'
    ) THEN
        ALTER TABLE network_zone.ip_range
            DROP CONSTRAINT IF EXISTS ip_range_pkey;

        ALTER TABLE network_zone.ip_range
            ADD CONSTRAINT ip_range_id_pkey PRIMARY KEY (id);
    END IF;
END $$;

CREATE TABLE IF NOT EXISTS network_zone.device_ip_range_root
(
    dev_id BIGINT NOT NULL,
    ip_range_id BIGINT NOT NULL,
    order_to_root BIGINT NOT NULL,
    PRIMARY KEY (ip_range_id, dev_id)
);

CREATE TABLE IF NOT EXISTS network_zone.device_ip_range_internet
(
    dev_id BIGINT NOT NULL,
    ip_range_id BIGINT NOT NULL,
    order_to_internet BIGINT NOT NULL,
    PRIMARY KEY (ip_range_id, dev_id)
);

ALTER TABLE network_zone.device_ip_range_root DROP CONSTRAINT IF EXISTS dev_id_device_ip_range_root;
ALTER TABLE network_zone.device_ip_range_root DROP CONSTRAINT IF EXISTS ip_range_id_device_ip_range_root;
ALTER TABLE network_zone.device_ip_range_internet DROP CONSTRAINT IF EXISTS dev_id_device_ip_range_internet;
ALTER TABLE network_zone.device_ip_range_internet DROP CONSTRAINT IF EXISTS ip_range_id_device_ip_range_internet;
ALTER TABLE network_zone.device_ip_range_root ADD CONSTRAINT dev_id_device_ip_range_root FOREIGN KEY (dev_id) REFERENCES device(dev_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE network_zone.device_ip_range_root ADD CONSTRAINT ip_range_id_device_ip_range_root FOREIGN KEY (ip_range_id) REFERENCES network_zone.ip_range(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE network_zone.device_ip_range_internet ADD CONSTRAINT dev_id_device_ip_range_internet FOREIGN KEY (dev_id) REFERENCES device(dev_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE network_zone.device_ip_range_internet ADD CONSTRAINT ip_range_id_device_ip_range_internet FOREIGN KEY (ip_range_id) REFERENCES network_zone.ip_range(id) ON UPDATE RESTRICT ON DELETE CASCADE;

CREATE INDEX IF NOT EXISTS idx_fkey_device_ip_range_root_dev_id
ON network_zone.device_ip_range_root (dev_id);
CREATE INDEX IF NOT EXISTS idx_fkey_device_ip_range_internet_dev_id
ON network_zone.device_ip_range_internet (dev_id);

CREATE UNIQUE INDEX IF NOT EXISTS idx_unique_order_to_root_per_ip_range
ON network_zone.device_ip_range_root (ip_range_id, order_to_root);
CREATE UNIQUE INDEX IF NOT EXISTS idx_unique_order_to_internet_per_ip_range
ON network_zone.device_ip_range_internet (ip_range_id, order_to_internet);
