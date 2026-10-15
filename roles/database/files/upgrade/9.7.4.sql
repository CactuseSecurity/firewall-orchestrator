-- Retain the pre-migration values once, including overrides no longer permitted
-- at their old levels and any nonstandard install-on targets. Review this JSON
-- backup when selecting the replacement overrides in provisioning settings.
-- This archive is intentionally not tracked in Hasura. Global config is readable
-- anonymously and must not contain these restricted provisioning values.
CREATE TABLE IF NOT EXISTS provisioning_config_backup
(
    version varchar PRIMARY KEY,
    config_values jsonb NOT NULL
);
INSERT INTO provisioning_config_backup (version, config_values)
SELECT '9.7.4',
       COALESCE(jsonb_agg(jsonb_build_object(
           'node_id', v.node_id, 'node_type', n.node_type,
           'object_key', n.object_key, 'config_key', v.config_key,
           'config_value', v.config_value)), '[]'::jsonb)
FROM provisioning_config_value v
JOIN provisioning_config_node n ON n.id = v.node_id
ON CONFLICT DO NOTHING;

-- Prefer an already stored canonical override if both old and new keys exist.
INSERT INTO provisioning_config_value (node_id, config_key, config_value)
SELECT node_id,
       CASE config_key
           WHEN 'logging' THEN 'ruleLoggingOverride'
           WHEN 'zoneFrom' THEN 'ruleZoneFromOverride'
           WHEN 'zoneTo' THEN 'ruleZoneToOverride'
       END, config_value
FROM provisioning_config_value
WHERE config_key IN ('logging', 'zoneFrom', 'zoneTo')
ON CONFLICT (node_id, config_key) DO NOTHING;
DELETE FROM provisioning_config_value WHERE config_key IN ('logging', 'zoneFrom', 'zoneTo');

-- The old None implementation mode becomes manual with planning disabled.
INSERT INTO provisioning_config_value (node_id, config_key, config_value)
SELECT node_id, 'planImplementation', 'false'::jsonb
FROM provisioning_config_value
WHERE config_key = 'implementationMode' AND config_value = '"None"'::jsonb
ON CONFLICT (node_id, config_key) DO NOTHING;

UPDATE provisioning_config_value
SET config_value = to_jsonb(CASE config_value #>> '{}'
    WHEN 'FwoAuto' THEN 'Automatic'
    WHEN 'TufinSc' THEN 'reroute_to_external_ticket_system'
    WHEN 'None' THEN 'Manual' END)
WHERE config_key = 'implementationMode' AND config_value #>> '{}' IN ('FwoAuto', 'TufinSc', 'None');

-- The old rule category becomes the common-service separation switch.
INSERT INTO provisioning_config_value (node_id, config_key, config_value)
SELECT node_id, 'commonServiceSeparation', to_jsonb(config_value #>> '{}' IN ('CommonService', 'common_service'))
FROM provisioning_config_value
WHERE config_key = 'ruleCategory'
ON CONFLICT (node_id, config_key) DO NOTHING;
DELETE FROM provisioning_config_value WHERE config_key = 'ruleCategory';

UPDATE provisioning_config_value v
SET config_value = to_jsonb(mapping.new_value)
FROM (VALUES
    ('ruleLoggingOverride', 'Log', 'log'),
    ('ruleLoggingOverride', 'LogTrack', 'log_track'),
    ('ruleLoggingOverride', 'None', 'none'),
    ('ruleType', 'AlwaysAccess', 'access'),
    ('ruleType', 'HandleAccessAndNat', 'accessAndNat'),
    ('ruleType', 'HandleAccessAndIps', 'accessAndIPS'),
    ('ruleType', 'HandleAccessNatIps', 'accessNatIPS'),
    ('positioningAlgorithm', 'CheckPointInlineLayerPerZonePair', 'cpInlineLayerPerZonePair'),
    ('positioningAlgorithm', 'FortinetEndOfZone', 'endOfZone'),
    ('positioningAlgorithm', 'CheckPointEndOfAppSection', 'endOfAppSection'),
    ('positioningAlgorithm', 'CheckPointEndOfAppSectionDistinguishCommonServices', 'endOfAppSectionDistinguishCommonServices')
) AS mapping(config_key, old_value, new_value)
WHERE v.config_key = mapping.config_key AND v.config_value #>> '{}' = mapping.old_value;

-- Only the two documented install-on choices remain active; other old targets
-- are preserved in the backup above for an administrator to reassess.
DELETE FROM provisioning_config_value
WHERE config_key = 'installOn' AND config_value NOT IN ('"ANY"'::jsonb, '"gatewaysInPath"'::jsonb);

-- Templates are now device-type settings only, and security profiles were never
-- global. Removed overrides are backed up above.
DELETE FROM provisioning_config_value v USING provisioning_config_node n
WHERE v.node_id = n.id AND (
    (n.node_type <> 'device_type' AND v.config_key = 'templates')
    OR (n.node_type = 'global' AND v.config_key = 'securityProfiles')
);
