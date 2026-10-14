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
SELECT '9.7.2',
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
SELECT node_id, 'planImplementation', '"Disable"'::jsonb
FROM provisioning_config_value
WHERE config_key = 'implementationMode' AND config_value = '"None"'::jsonb
ON CONFLICT (node_id, config_key) DO NOTHING;

UPDATE provisioning_config_value
SET config_value = to_jsonb(CASE config_value #>> '{}'
    WHEN 'TufinSc' THEN 'reroute_to_external_ticket_system'
    WHEN 'None' THEN 'Manual' END)
WHERE config_key = 'implementationMode' AND config_value #>> '{}' IN ('TufinSc', 'None');

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
    ('positioningAlgorithm', 'CheckPointEndOfAppSectionDistinguishCommonServices', 'endOfAppSectionDistinguishCommonServices'),
    ('ruleCategory', 'App', 'app'),
    ('ruleCategory', 'CommonService', 'common_service')
) AS mapping(config_key, old_value, new_value)
WHERE v.config_key = mapping.config_key AND v.config_value #>> '{}' = mapping.old_value;

-- Only the two documented install-on choices remain active; other old targets
-- are preserved in the backup above for an administrator to reassess.
DELETE FROM provisioning_config_value
WHERE config_key = 'installOn' AND config_value NOT IN ('"ANY"'::jsonb, '"gatewaysInPath"'::jsonb);

-- Overrides outside their newly permitted scopes are backed up above. Settings
-- inherited from permitted ancestors remain effective for all descendant levels.
DELETE FROM provisioning_config_value v USING provisioning_config_node n
WHERE v.node_id = n.id AND (
    (n.node_type = 'gateway' AND v.config_key IN (
        'serviceObjectCreation', 'addressObjectCreation', 'timeObjectCreation',
        'labelCreation', 'ruleCategory', 'ownerIdCustomFieldName',
        'changeNumberCustomFieldName', 'modellingCreateAppZones',
        'modellingImplementNetworkAreaObjects', 'modellingDynIpLabelsVisibleAsGroups', 'templates'))
    OR (n.node_type = 'global' AND v.config_key IN ('securityProfiles', 'templates'))
    OR (n.node_type <> 'global' AND v.config_key = 'modellingNamingConventions')
);
-- Implementation tasks may be implemented on a different management than their request task, e.g. an object
-- task on every sub-management of a super-management. NULL keeps the previous behaviour: the task is implemented
-- on the management of its request task.
ALTER TABLE request.impltask ADD COLUMN IF NOT EXISTS mgm_id int;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'request_impltask_management_foreign_key'
        AND conrelid = 'request.impltask'::regclass
    ) THEN
        ALTER TABLE request.impltask
        ADD CONSTRAINT request_impltask_management_foreign_key
        FOREIGN KEY (mgm_id) REFERENCES management (mgm_id)
        ON UPDATE RESTRICT ON DELETE CASCADE;
    END IF;
END $$;

-- GHSA-695m-f6mv-9g72 (B): a local user is identified by its directory and dn, but a dn can be handed to another
-- person, e.g. when an account is deleted and a later one gets the same name. The new account then took over the
-- local user with its reports, templates, schedules and workflow history, and a still running session of the old
-- account was refreshed with the rights of the new one. The local user is now bound to the immutable object id of
-- its directory entry (objectGUID, entryUUID, nsUniqueId, ...), which the middleware records at the next password
-- login. Afterwards a different object id under the same dn is refused until an administrator resolves it.
ALTER TABLE uiuser ADD COLUMN IF NOT EXISTS directory_object_id Varchar;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'uiuser_ldap_connection_id_directory_object_id_key' AND conrelid = 'uiuser'::regclass
    ) THEN
        ALTER TABLE uiuser ADD CONSTRAINT uiuser_ldap_connection_id_directory_object_id_key UNIQUE (ldap_connection_id, directory_object_id);
    END IF;
END $$;

-- A refresh renews a session without a password, so it cannot bind a local user that has no object id yet: the
-- dn might already name another person. Sessions of such users therefore end, and the next password login binds
-- the local user. Users that are bound already keep their sessions, so repeating the upgrade changes nothing.
UPDATE refresh_token
SET revoked_at = now()
WHERE revoked_at IS NULL
    AND user_id IN (SELECT uiuser_id FROM uiuser WHERE directory_object_id IS NULL);

-- An LDAP connection lists all servers of its directory (ldap_server becomes an array), which are tried in order.
-- Replicas of one directory used to be configured as separate connections, which the disjoint paths below no
-- longer allow; they belong into the server list of one connection now. Existing connections keep their server.
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'ldap_connection' AND column_name = 'ldap_server' AND data_type <> 'ARRAY'
    ) THEN
        ALTER TABLE ldap_connection ALTER COLUMN ldap_server TYPE Varchar[] USING ARRAY[ldap_server];
    END IF;
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'ldap_connection_server_not_empty' AND conrelid = 'ldap_connection'::regclass
    ) THEN
        ALTER TABLE ldap_connection ADD CONSTRAINT ldap_connection_server_not_empty
            CHECK (cardinality(ldap_server) > 0 AND array_position(ldap_server, NULL) IS NULL AND array_position(ldap_server, '') IS NULL);
    END IF;
END $$;

-- GHSA-695m-f6mv-9g72 (A): roles, groups, owner responsibles, workflow visibility groups and workflow assignments
-- refer to subjects by their bare dn, which is unique only inside one directory. Each active LDAP connection now
-- owns the subtrees below its search and write paths, the more specific path winning for nested paths. A dn below
-- the same path of two active connections belongs to neither, and its users can no longer log in. Name the
-- connections sharing a path, so that their paths can be made distinct (Settings - LDAP Connections). The
-- comparison ignores case and blanks around separators.
DO $$
DECLARE
    v_overlaps TEXT;
BEGIN
    WITH ldap_path AS (
        SELECT ldap_connection_id, lower(regexp_replace(path, '\s*([,=])\s*', '\1', 'g')) AS path
        FROM ldap_connection,
            unnest(ARRAY[ldap_searchpath_for_users, ldap_searchpath_for_roles, ldap_searchpath_for_groups, ldap_writepath_for_groups]) AS path
        WHERE active AND COALESCE(btrim(path), '') <> ''
    )
    SELECT string_agg(DISTINCT first_path.ldap_connection_id || '/' || second_path.ldap_connection_id, ', ')
    INTO v_overlaps
    FROM ldap_path first_path
    JOIN ldap_path second_path ON first_path.ldap_connection_id < second_path.ldap_connection_id
    WHERE first_path.path = second_path.path;

    IF v_overlaps IS NOT NULL THEN
        RAISE WARNING 'ldap_connection: the active LDAP connection pairs % share a search or write path. Users and groups below a shared path can no longer log in or be resolved. Give the active connections distinct paths (Settings - LDAP Connections). Connections to replicas of the same directory have to be combined into one connection listing all of their servers.', v_overlaps;
    END IF;
END $$;
