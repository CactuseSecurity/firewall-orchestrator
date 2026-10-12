-- Run with psql -v upgrade_file=/absolute/path/to/upgrade/9.7.2.sql -f this-file.
-- An isolated schema and temporary tables protect production data; the transaction is rolled back.
-- This probes legacy renames, canonical-key conflicts, restricted scopes,
-- option conversion, original-data backup and repeat execution.
\set ON_ERROR_STOP 1
BEGIN;
CREATE SCHEMA provisioning_upgrade_9_7_2_test;
SET LOCAL search_path = provisioning_upgrade_9_7_2_test, pg_temp;
CREATE TEMP TABLE config (config_key varchar, config_value varchar, config_user integer,
    PRIMARY KEY (config_key, config_user));
INSERT INTO config VALUES ('pathAnalysisAlgorithm', '2', 0);
CREATE TEMP TABLE original_config AS SELECT * FROM config;
CREATE TEMP TABLE provisioning_config_node (id bigint PRIMARY KEY, node_type varchar, object_key varchar);
CREATE TEMP TABLE provisioning_config_value (node_id bigint, config_key varchar, config_value jsonb,
    PRIMARY KEY (node_id, config_key));
INSERT INTO provisioning_config_node VALUES
    (1, 'global', 'global'), (2, 'device_type', '10'), (3, 'management', '100'), (4, 'gateway', '200');
INSERT INTO provisioning_config_value VALUES
    (1, 'logging', '"Log"'), (1, 'zoneFrom', '"internal"'),
    (1, 'ruleType', '"AlwaysAccess"'), (1, 'ruleCategory', '"App"'),
    (1, 'positioningAlgorithm', '"CheckPointEndOfAppSectionDistinguishCommonServices"'),
    (1, 'implementationMode', '"None"'), (1, 'planImplementation', '"Enable"'),
    (1, 'installOn', '"custom-cluster"'), (1, 'templates', '"{}"'),
    (2, 'logging', '"Log"'), (2, 'ruleLoggingOverride', '"LogTrack"'),
    (2, 'zoneTo', '"ANY"'), (2, 'implementationMode', '"TufinSc"'),
    (2, 'securityProfiles', '["strict"]'), (2, 'templates', '"template"'),
    (3, 'implementationMode', '"None"'), (3, 'addressObjectCreation', '"Submanager"'),
    (3, 'ruleType', '"HandleAccessAndIps"'),
    (4, 'serviceObjectCreation', '"Submanager"'), (4, 'addressObjectCreation', '"Submanager"'),
    (4, 'ruleCategory', '"CommonService"'), (4, 'templates', '"gateway-template"'),
    (4, 'logging', '"None"'), (4, 'installOn', '"gatewaysInPath"');
CREATE TEMP TABLE original_values AS SELECT * FROM provisioning_config_value;

\i :upgrade_file

CREATE TEMP TABLE expected_values (LIKE provisioning_config_value);
INSERT INTO expected_values VALUES
    (1, 'ruleLoggingOverride', '"log"'), (1, 'ruleZoneFromOverride', '"internal"'),
    (1, 'ruleType', '"access"'), (1, 'ruleCategory', '"app"'),
    (1, 'positioningAlgorithm', '"endOfAppSectionDistinguishCommonServices"'),
    (1, 'implementationMode', '"Manual"'), (1, 'planImplementation', '"Enable"'),
    (2, 'ruleLoggingOverride', '"log_track"'), (2, 'ruleZoneToOverride', '"ANY"'),
    (2, 'implementationMode', '"reroute_to_external_ticket_system"'),
    (2, 'securityProfiles', '["strict"]'), (2, 'templates', '"template"'),
    (3, 'implementationMode', '"Manual"'), (3, 'planImplementation', '"Disable"'),
    (3, 'addressObjectCreation', '"Submanager"'), (3, 'ruleType', '"accessAndIPS"'),
    (4, 'ruleLoggingOverride', '"none"'), (4, 'installOn', '"gatewaysInPath"');
CREATE FUNCTION pg_temp.assert_migration() RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    IF EXISTS ((SELECT * FROM config EXCEPT SELECT * FROM original_config)
        UNION ALL (SELECT * FROM original_config EXCEPT SELECT * FROM config)) THEN
        RAISE EXCEPTION 'General settings must stay unchanged and must not contain provisioning backups';
    END IF;
    IF EXISTS ((SELECT * FROM provisioning_config_value EXCEPT SELECT * FROM expected_values)
        UNION ALL (SELECT * FROM expected_values EXCEPT SELECT * FROM provisioning_config_value)) THEN
        RAISE EXCEPTION 'Migrated values differ from the expected contract';
    END IF;
    IF EXISTS ((SELECT node_id, config_key, config_value FROM original_values
        EXCEPT SELECT (item->>'node_id')::bigint, item->>'config_key', item->'config_value'
        FROM provisioning_config_backup, jsonb_array_elements(config_values) item
        WHERE version = '9.7.2')
        UNION ALL (SELECT (item->>'node_id')::bigint, item->>'config_key', item->'config_value'
        FROM provisioning_config_backup, jsonb_array_elements(config_values) item
        WHERE version = '9.7.2'
        EXCEPT SELECT node_id, config_key, config_value FROM original_values)) THEN
        RAISE EXCEPTION 'Original values were not preserved in the backup';
    END IF;
END $$;
SELECT pg_temp.assert_migration();

\i :upgrade_file
SELECT pg_temp.assert_migration();
ROLLBACK;
\echo '9.7.2 provisioning migration and idempotency checks passed'
