-- external requests that write an implementation task directly to the firewall reference it; null = request task based (e.g. Tufin)
ALTER TABLE ext_request
    ADD COLUMN IF NOT EXISTS impltask_id bigint;

-- A request keeps its history when its implementation task is deleted: the reference is cleared (ON DELETE SET NULL)
-- and the sender discards the request. A constraint created by an earlier run with ON DELETE CASCADE is replaced.
DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conrelid = 'public.ext_request'::regclass
            AND conname = 'ext_request_impltask_id_foreign_key'
            AND confdeltype <> 'n'
    ) THEN
        ALTER TABLE ext_request DROP CONSTRAINT ext_request_impltask_id_foreign_key;
    END IF;
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conrelid = 'public.ext_request'::regclass
            AND conname = 'ext_request_impltask_id_foreign_key'
    ) THEN
        ALTER TABLE ext_request
            ADD CONSTRAINT ext_request_impltask_id_foreign_key
            FOREIGN KEY (impltask_id) REFERENCES request.impltask(id) ON UPDATE RESTRICT ON DELETE SET NULL;
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS idx_ext_request_impltask_id ON ext_request (impltask_id);

-- External requests that hand request tasks to an external ticket system or write implementation tasks to the
-- firewall are created by the state action "Create external requests". It is provided once, but not attached to a
-- state: an administrator decides where it runs, e.g. after "Create implementation tasks".
INSERT INTO request.action (name, action_type, scope, task_type, phase, event, button_text, external_parameters)
SELECT 'Create external requests', 'CreateExtRequests', 'Ticket', '', '', 'OnSet', '', ''
WHERE NOT EXISTS (SELECT 1 FROM request.action WHERE action_type = 'CreateExtRequests');

-- The device specific setting for the implementation mode of rules is named like the one for objects; each applies
-- to its kind of task only.
UPDATE device_specific_config_value
SET config_key = 'implementationModeForRules'
WHERE config_key = 'implementationModeOverrideForRules'
    AND node_id NOT IN (SELECT node_id FROM device_specific_config_value WHERE config_key = 'implementationModeForRules');
DELETE FROM device_specific_config_value WHERE config_key = 'implementationModeOverrideForRules';

-- How firewall config changes are implemented moves from the setting "Firewall config changes - General"
-- (fwConfigChangeMgmSettings) to the device specific settings implementationMode (objects), implementationModeForRules
-- (rules) and rerouteToExternalTicketSystem. The old setting decided the routing of external requests so far, so it
-- wins over differing settings: per management and change category, a Tufin SecureChange system becomes the external
-- ticket system mode with that system (rules decide the system if both categories go to different systems), a Check
-- Point system becomes Automatic (written by the firewall write library), Internal Work, Disabled, a disabled
-- management and any other ticket system become Manual. A value is only stored on the management if the value in
-- effect there (own, device type or global) differs, so the levels above keep deciding where they already agree.
-- Managements missing in the old setting are left unchanged and inherit; a change category without a target of a
-- listed management becomes Manual, as FWO did not implement it before.
-- Before, the rule mode is taken from the object mode on every level that only had the object mode (9.7.4 migrated
-- the object mode only). The previous values and the old setting are backed up, the old setting is removed; the backup
-- marks the migration as done, so a repeated upgrade keeps later changes of an administrator.
DO $$
DECLARE
    t_mgm_settings text;
    t_systems text;
    j_mgm_settings jsonb;
    j_systems jsonb;
    i_global_node bigint;
    i_device_type_node bigint;
    i_management_node bigint;
    j_effective jsonb;
    r_route RECORD;
    r_value RECORD;
BEGIN
    IF EXISTS (SELECT 1 FROM device_specific_config_backup WHERE version = '9.8.7 implementation modes') THEN
        RETURN;
    END IF;

    SELECT config_value INTO t_mgm_settings FROM config WHERE config_key = 'fwConfigChangeMgmSettings' AND config_user = 0;
    BEGIN
        j_mgm_settings := COALESCE(NULLIF(btrim(COALESCE(t_mgm_settings, '')), ''), '[]')::jsonb;
    EXCEPTION WHEN invalid_text_representation THEN
        j_mgm_settings := '[]'::jsonb;
    END;
    IF jsonb_typeof(j_mgm_settings) <> 'array' THEN
        j_mgm_settings := '[]'::jsonb;
    END IF;
    SELECT config_value INTO t_systems FROM config WHERE config_key = 'extTicketSystems' AND config_user = 0;
    BEGIN
        j_systems := COALESCE(NULLIF(btrim(COALESCE(t_systems, '')), ''), '[]')::jsonb;
    EXCEPTION WHEN invalid_text_representation THEN
        j_systems := '[]'::jsonb;
    END;
    IF jsonb_typeof(j_systems) <> 'array' THEN
        j_systems := '[]'::jsonb;
    END IF;

    INSERT INTO device_specific_config_backup (version, config_values)
    SELECT '9.8.7 implementation modes', jsonb_build_object(
        'fwConfigChangeMgmSettings', j_mgm_settings,
        'values', COALESCE(jsonb_agg(jsonb_build_object('node_type', n.node_type, 'object_key', n.object_key,
            'config_key', v.config_key, 'config_value', v.config_value) ORDER BY n.id, v.config_key), '[]'::jsonb))
    FROM device_specific_config_value v
    JOIN device_specific_config_node n ON n.id = v.node_id
    WHERE v.config_key IN ('implementationMode', 'implementationModeForRules', 'rerouteToExternalTicketSystem');

    INSERT INTO device_specific_config_value (node_id, config_key, config_value)
    SELECT node_id, 'implementationModeForRules', to_jsonb(CASE config_value #>> '{}'
        WHEN 'reroute_to_external_ticket_system' THEN 'rerouteToExternalTicketSystem' ELSE config_value #>> '{}' END)
    FROM device_specific_config_value
    WHERE config_key = 'implementationMode' AND config_value #>> '{}' IN ('Automatic', 'Manual', 'reroute_to_external_ticket_system')
    ON CONFLICT (node_id, config_key) DO NOTHING;

    DROP TABLE IF EXISTS tmp_impl_mode_route;
    DROP TABLE IF EXISTS tmp_impl_mode_target;

    -- target per existing management and change category: External, Automatic or Manual. A category without target was
    -- not implemented by FWO before, so it becomes Manual instead of inheriting (Automatic would write it directly now)
    CREATE TEMP TABLE tmp_impl_mode_route AS
    SELECT m.mgm_id, m.mgm_name, m.dev_typ_id, c.category, c.config_key,
        CASE
            WHEN COALESCE(ms->>'Enabled', 'true') = 'false' THEN 'Manual'
            WHEN COALESCE(ms->'SelectedChanges'->>c.category, '') = '' THEN 'Manual'
            WHEN ms->'SelectedChanges'->>c.category !~ '^[0-9]{1,9}$' THEN 'Manual'
            WHEN sys.type_id = '2' THEN 'External'
            WHEN sys.type_id IN ('9', '13') THEN 'Automatic'
            ELSE 'Manual'
        END AS target,
        CASE WHEN COALESCE(ms->>'Enabled', 'true') <> 'false' AND sys.type_id = '2' THEN (ms->'SelectedChanges'->>c.category)::integer END AS system_id
    FROM jsonb_array_elements(j_mgm_settings) ms
    JOIN management m ON m.mgm_id = CASE WHEN (ms->>'Id') ~ '^[0-9]{1,9}$' THEN (ms->>'Id')::integer END
    CROSS JOIN (VALUES ('Object Changes', 'implementationMode'), ('Rule Changes', 'implementationModeForRules')) c (category, config_key)
    LEFT JOIN LATERAL (
        SELECT COALESCE(s->>'TypeId', '1') AS type_id
        FROM jsonb_array_elements(j_systems) s
        WHERE jsonb_typeof(ms->'SelectedChanges') = 'object' AND s->>'Id' = ms->'SelectedChanges'->>c.category
        LIMIT 1
    ) sys ON true
    WHERE jsonb_typeof(ms) = 'object';

    -- values to put into effect on each management; the system of the rules wins over that of the objects
    CREATE TEMP TABLE tmp_impl_mode_target AS
    SELECT mgm_id, mgm_name, dev_typ_id, config_key, to_jsonb(CASE target
            WHEN 'External' THEN CASE config_key WHEN 'implementationMode' THEN 'reroute_to_external_ticket_system'
                ELSE 'rerouteToExternalTicketSystem' END
            ELSE target END) AS config_value
    FROM tmp_impl_mode_route
    WHERE target IS NOT NULL
    UNION ALL
    SELECT * FROM (
        SELECT DISTINCT ON (mgm_id) mgm_id, mgm_name, dev_typ_id, 'rerouteToExternalTicketSystem' AS config_key,
            to_jsonb(system_id) AS config_value
        FROM tmp_impl_mode_route
        WHERE system_id IS NOT NULL
        ORDER BY mgm_id, CASE config_key WHEN 'implementationModeForRules' THEN 0 ELSE 1 END
    ) rule_system_first;

    INSERT INTO device_specific_config_node (node_type, object_key, display_name, sort_order)
    VALUES ('global', 'global', 'Global', 0)
    ON CONFLICT (node_type, object_key) DO NOTHING;
    SELECT id INTO i_global_node FROM device_specific_config_node WHERE node_type = 'global';

    FOR r_route IN
        SELECT DISTINCT t.mgm_id, t.mgm_name, t.dev_typ_id, concat_ws(' ', d.dev_typ_name, d.dev_typ_version) AS device_type_name
        FROM tmp_impl_mode_target t
        JOIN stm_dev_typ d ON d.dev_typ_id = t.dev_typ_id
        ORDER BY t.mgm_id
    LOOP
        i_device_type_node := NULL;
        i_management_node := NULL;
        SELECT id INTO i_device_type_node FROM device_specific_config_node
        WHERE node_type = 'device_type' AND object_key = r_route.dev_typ_id::text;
        SELECT id INTO i_management_node FROM device_specific_config_node
        WHERE node_type = 'management' AND object_key = r_route.mgm_id::text;

        FOR r_value IN
            SELECT config_key, config_value FROM tmp_impl_mode_target WHERE mgm_id = r_route.mgm_id ORDER BY config_key
        LOOP
            SELECT COALESCE(
                (SELECT config_value FROM device_specific_config_value WHERE node_id = i_management_node AND config_key = r_value.config_key),
                (SELECT config_value FROM device_specific_config_value WHERE node_id = i_device_type_node AND config_key = r_value.config_key),
                (SELECT config_value FROM device_specific_config_value WHERE node_id = i_global_node AND config_key = r_value.config_key),
                -- defaults of an unset level: both modes Automatic, no external ticket system
                CASE WHEN r_value.config_key <> 'rerouteToExternalTicketSystem' THEN '"Automatic"'::jsonb END)
            INTO j_effective;
            CONTINUE WHEN j_effective IS NOT DISTINCT FROM r_value.config_value;

            IF i_management_node IS NULL THEN
                INSERT INTO device_specific_config_node (node_type, object_key, parent_id, display_name)
                VALUES ('device_type', r_route.dev_typ_id::text, i_global_node, r_route.device_type_name)
                ON CONFLICT (node_type, object_key) DO NOTHING;
                SELECT id INTO i_device_type_node FROM device_specific_config_node
                WHERE node_type = 'device_type' AND object_key = r_route.dev_typ_id::text;
                INSERT INTO device_specific_config_node (node_type, object_key, parent_id, display_name)
                VALUES ('management', r_route.mgm_id::text, i_device_type_node, r_route.mgm_name)
                ON CONFLICT (node_type, object_key) DO NOTHING;
                SELECT id INTO i_management_node FROM device_specific_config_node
                WHERE node_type = 'management' AND object_key = r_route.mgm_id::text;
            END IF;
            INSERT INTO device_specific_config_value (node_id, config_key, config_value)
            VALUES (i_management_node, r_value.config_key, r_value.config_value)
            ON CONFLICT (node_id, config_key) DO UPDATE SET config_value = EXCLUDED.config_value;
        END LOOP;
    END LOOP;

    DELETE FROM config WHERE config_key = 'fwConfigChangeMgmSettings';

    DROP TABLE tmp_impl_mode_route;
    DROP TABLE tmp_impl_mode_target;
END $$;

-- External requests take their response timeout and retries from the device specific settings requestResponseTimeout,
-- requestMaxAttempts and requestCyclesBetweenAttempts instead of the ticket system they are sent to. Each management
-- keeps the values of the ticket system it uses: the external ticket system selected for it, otherwise the Check Point
-- system the former setting "Firewall config changes - General" routed it to (its values now apply to the firewall
-- writes). The values of the system most managements use become global, where they differ from the defaults (300
-- seconds, 3 attempts, 5 cycles); a management using another system gets the values of that system that differ. The
-- values of the ticket systems are backed up, which also marks the migration as done.
DO $$
DECLARE
    t_systems text;
    j_systems jsonb;
    j_old_routes jsonb;
    i_global_node bigint;
    i_device_type_node bigint;
    i_management_node bigint;
    i_global_system integer;
    r_route RECORD;
    r_value RECORD;
BEGIN
    IF EXISTS (SELECT 1 FROM device_specific_config_backup WHERE version = '9.8.7 request settings') THEN
        RETURN;
    END IF;

    SELECT config_value INTO t_systems FROM config WHERE config_key = 'extTicketSystems' AND config_user = 0;
    BEGIN
        j_systems := COALESCE(NULLIF(btrim(COALESCE(t_systems, '')), ''), '[]')::jsonb;
    EXCEPTION WHEN invalid_text_representation THEN
        j_systems := '[]'::jsonb;
    END;
    IF jsonb_typeof(j_systems) <> 'array' THEN
        j_systems := '[]'::jsonb;
    END IF;
    SELECT config_values->'fwConfigChangeMgmSettings' INTO j_old_routes
    FROM device_specific_config_backup WHERE version = '9.8.7 implementation modes';
    IF jsonb_typeof(j_old_routes) IS DISTINCT FROM 'array' THEN
        j_old_routes := '[]'::jsonb;
    END IF;

    INSERT INTO device_specific_config_backup (version, config_values)
    SELECT '9.8.7 request settings', COALESCE(jsonb_agg(jsonb_build_object('Id', s->'Id', 'Name', s->'Name', 'TypeId', s->'TypeId',
        'ResponseTimeout', s->'ResponseTimeout', 'MaxAttempts', s->'MaxAttempts', 'CyclesBetweenAttempts', s->'CyclesBetweenAttempts')
        ORDER BY ord), '[]'::jsonb)
    FROM jsonb_array_elements(j_systems) WITH ORDINALITY e(s, ord)
    WHERE jsonb_typeof(s) = 'object';

    DROP TABLE IF EXISTS tmp_request_value;
    DROP TABLE IF EXISTS tmp_request_route;

    -- the values of each ticket system; a missing value is the default of the ticket system, which equals the setting default
    CREATE TEMP TABLE tmp_request_value AS
    SELECT system_id, config_key, value, default_value
    FROM (
        SELECT (s->>'Id')::integer AS system_id, k.config_key, k.minimum, k.default_value,
            CASE WHEN s->>k.property IS NULL THEN k.default_value
                WHEN (s->>k.property) ~ '^[0-9]{1,9}$' THEN (s->>k.property)::integer END AS value
        FROM jsonb_array_elements(j_systems) s
        CROSS JOIN (VALUES ('requestResponseTimeout', 'ResponseTimeout', 1, 300),
            ('requestMaxAttempts', 'MaxAttempts', 1, 3),
            ('requestCyclesBetweenAttempts', 'CyclesBetweenAttempts', 0, 5)) k (config_key, property, minimum, default_value)
        WHERE jsonb_typeof(s) = 'object' AND (s->>'Id') ~ '^[0-9]{1,9}$'
    ) system_values
    WHERE value >= minimum;

    -- the ticket system each management uses: the selected external ticket system in effect, otherwise its Check Point system
    CREATE TEMP TABLE tmp_request_route AS
    SELECT m.mgm_id, m.mgm_name, m.dev_typ_id, COALESCE(selected.system_id, check_point.system_id) AS system_id
    FROM management m
    LEFT JOIN LATERAL (
        SELECT (effective.value #>> '{}')::integer AS system_id
        FROM (SELECT COALESCE(
            (SELECT v.config_value FROM device_specific_config_value v JOIN device_specific_config_node n ON n.id = v.node_id
                WHERE n.node_type = 'management' AND n.object_key = m.mgm_id::text AND v.config_key = 'rerouteToExternalTicketSystem'),
            (SELECT v.config_value FROM device_specific_config_value v JOIN device_specific_config_node n ON n.id = v.node_id
                WHERE n.node_type = 'device_type' AND n.object_key = m.dev_typ_id::text AND v.config_key = 'rerouteToExternalTicketSystem'),
            (SELECT v.config_value FROM device_specific_config_value v JOIN device_specific_config_node n ON n.id = v.node_id
                WHERE n.node_type = 'global' AND v.config_key = 'rerouteToExternalTicketSystem')) AS value) effective
        WHERE (effective.value #>> '{}') ~ '^[0-9]{1,9}$'
    ) selected ON true
    LEFT JOIN LATERAL (
        SELECT min(c.value::integer) AS system_id
        FROM jsonb_array_elements(j_old_routes) ms
        CROSS JOIN LATERAL jsonb_each_text(CASE WHEN jsonb_typeof(ms->'SelectedChanges') = 'object'
            THEN ms->'SelectedChanges' ELSE '{}'::jsonb END) c
        JOIN jsonb_array_elements(j_systems) s ON jsonb_typeof(s) = 'object' AND s->>'Id' = c.value
            AND COALESCE(s->>'TypeId', '1') IN ('9', '13')
        WHERE jsonb_typeof(ms) = 'object' AND ms->>'Id' = m.mgm_id::text AND COALESCE(ms->>'Enabled', 'true') <> 'false'
            AND c.value ~ '^[0-9]{1,9}$'
    ) check_point ON true
    WHERE COALESCE(selected.system_id, check_point.system_id) IN (SELECT system_id FROM tmp_request_value);

    SELECT system_id INTO i_global_system FROM tmp_request_route
    GROUP BY system_id ORDER BY count(*) DESC, system_id LIMIT 1;

    INSERT INTO device_specific_config_node (node_type, object_key, display_name, sort_order)
    VALUES ('global', 'global', 'Global', 0)
    ON CONFLICT (node_type, object_key) DO NOTHING;
    SELECT id INTO i_global_node FROM device_specific_config_node WHERE node_type = 'global';

    INSERT INTO device_specific_config_value (node_id, config_key, config_value)
    SELECT i_global_node, config_key, to_jsonb(value)
    FROM tmp_request_value WHERE system_id = i_global_system AND value <> default_value
    ON CONFLICT (node_id, config_key) DO NOTHING;

    FOR r_route IN
        SELECT r.mgm_id, r.mgm_name, r.dev_typ_id, r.system_id, concat_ws(' ', d.dev_typ_name, d.dev_typ_version) AS device_type_name
        FROM tmp_request_route r
        JOIN stm_dev_typ d ON d.dev_typ_id = r.dev_typ_id
        WHERE r.system_id <> i_global_system
        ORDER BY r.mgm_id
    LOOP
        i_management_node := NULL;
        FOR r_value IN
            SELECT own.config_key, own.value
            FROM tmp_request_value own
            LEFT JOIN tmp_request_value glob ON glob.system_id = i_global_system AND glob.config_key = own.config_key
            WHERE own.system_id = r_route.system_id AND own.value <> COALESCE(glob.value, own.default_value)
            ORDER BY own.config_key
        LOOP
            IF i_management_node IS NULL THEN
                INSERT INTO device_specific_config_node (node_type, object_key, parent_id, display_name)
                VALUES ('device_type', r_route.dev_typ_id::text, i_global_node, r_route.device_type_name)
                ON CONFLICT (node_type, object_key) DO NOTHING;
                SELECT id INTO i_device_type_node FROM device_specific_config_node
                WHERE node_type = 'device_type' AND object_key = r_route.dev_typ_id::text;
                INSERT INTO device_specific_config_node (node_type, object_key, parent_id, display_name)
                VALUES ('management', r_route.mgm_id::text, i_device_type_node, r_route.mgm_name)
                ON CONFLICT (node_type, object_key) DO NOTHING;
                SELECT id INTO i_management_node FROM device_specific_config_node
                WHERE node_type = 'management' AND object_key = r_route.mgm_id::text;
            END IF;
            INSERT INTO device_specific_config_value (node_id, config_key, config_value)
            VALUES (i_management_node, r_value.config_key, to_jsonb(r_value.value))
            ON CONFLICT (node_id, config_key) DO NOTHING;
        END LOOP;
    END LOOP;

    DROP TABLE tmp_request_value;
    DROP TABLE tmp_request_route;
END $$;

-- The device specific setting rerouteToExternalTicketSystem names the kind of external ticket system instead of an
-- entry of the ticket system list, and the connection to Tufin SecureChange (scUrl, scCredential, scLookupRequesterId)
-- moves from the ticket system list to the device specific settings. A level selecting a Tufin SecureChange system
-- selects tufinSecureChange; a level selecting another or an unknown system loses the selection, and its implementation
-- modes in effect for external ticket systems become Manual, as only Tufin SecureChange takes over tasks. The
-- connection of the system most managements use becomes global, a management using another one gets its differing
-- values; a system without credential (legacy authorization) inherits the credential only on the server of the global
-- system, on another server it is not taken over and its levels become Manual. The previous selections are backed up,
-- which also marks the migration as done.
DO $$
DECLARE
    t_systems text;
    j_systems jsonb;
    i_global_node bigint;
    i_device_type_node bigint;
    i_management_node bigint;
    i_global_system integer;
    i_ancestor bigint;
    j_effective jsonb;
    i_depth integer;
    r_selection RECORD;
    r_mode RECORD;
    r_route RECORD;
    r_value RECORD;
BEGIN
    IF EXISTS (SELECT 1 FROM device_specific_config_backup WHERE version = '9.8.7 securechange connection') THEN
        RETURN;
    END IF;

    SELECT config_value INTO t_systems FROM config WHERE config_key = 'extTicketSystems' AND config_user = 0;
    BEGIN
        j_systems := COALESCE(NULLIF(btrim(COALESCE(t_systems, '')), ''), '[]')::jsonb;
    EXCEPTION WHEN invalid_text_representation THEN
        j_systems := '[]'::jsonb;
    END;
    IF jsonb_typeof(j_systems) <> 'array' THEN
        j_systems := '[]'::jsonb;
    END IF;

    INSERT INTO device_specific_config_backup (version, config_values)
    SELECT '9.8.7 securechange connection', COALESCE(jsonb_agg(jsonb_build_object('node_type', n.node_type, 'object_key', n.object_key,
        'config_value', v.config_value) ORDER BY n.id), '[]'::jsonb)
    FROM device_specific_config_value v
    JOIN device_specific_config_node n ON n.id = v.node_id
    WHERE v.config_key = 'rerouteToExternalTicketSystem';

    DROP TABLE IF EXISTS tmp_sc_system;
    DROP TABLE IF EXISTS tmp_sc_connection_route;

    -- connection of every Tufin SecureChange system
    CREATE TEMP TABLE tmp_sc_system AS
    SELECT (s->>'Id')::integer AS system_id, COALESCE(s->>'Url', '') AS url,
        CASE WHEN (s->>'CredentialId') ~ '^[0-9]{1,9}$' AND (s->>'CredentialId')::integer > 0 THEN (s->>'CredentialId')::integer END AS credential_id,
        COALESCE(s->>'LookupRequesterId', 'false') = 'true' AS lookup_requester_id
    FROM jsonb_array_elements(j_systems) s
    WHERE jsonb_typeof(s) = 'object' AND (s->>'Id') ~ '^[0-9]{1,9}$' AND s->>'TypeId' = '2';

    -- the Tufin SecureChange system in effect for each management, before the selections are converted
    CREATE TEMP TABLE tmp_sc_connection_route AS
    SELECT m.mgm_id, m.mgm_name, m.dev_typ_id, (effective.value #>> '{}')::integer AS system_id
    FROM management m
    CROSS JOIN LATERAL (SELECT COALESCE(
        (SELECT v.config_value FROM device_specific_config_value v JOIN device_specific_config_node n ON n.id = v.node_id
            WHERE n.node_type = 'management' AND n.object_key = m.mgm_id::text AND v.config_key = 'rerouteToExternalTicketSystem'),
        (SELECT v.config_value FROM device_specific_config_value v JOIN device_specific_config_node n ON n.id = v.node_id
            WHERE n.node_type = 'device_type' AND n.object_key = m.dev_typ_id::text AND v.config_key = 'rerouteToExternalTicketSystem'),
        (SELECT v.config_value FROM device_specific_config_value v JOIN device_specific_config_node n ON n.id = v.node_id
            WHERE n.node_type = 'global' AND v.config_key = 'rerouteToExternalTicketSystem')) AS value) effective
    WHERE (effective.value #>> '{}') ~ '^[0-9]{1,9}$'
        AND (effective.value #>> '{}')::integer IN (SELECT system_id FROM tmp_sc_system);

    SELECT system_id INTO i_global_system FROM tmp_sc_connection_route
    GROUP BY system_id ORDER BY count(*) DESC, system_id LIMIT 1;

    -- A system without credential (legacy authorization) on another server than the global system would log in with
    -- the inherited credential of the global system, sending it to that other server. It is not taken over: its levels are
    -- treated like levels selecting an unknown system below.
    IF i_global_system IS NOT NULL THEN
        DELETE FROM tmp_sc_system own
        WHERE own.credential_id IS NULL AND own.system_id <> i_global_system
            AND own.url IS DISTINCT FROM (SELECT glob.url FROM tmp_sc_system glob WHERE glob.system_id = i_global_system);
        DELETE FROM tmp_sc_connection_route WHERE system_id NOT IN (SELECT system_id FROM tmp_sc_system);
    END IF;

    -- levels selecting another or an unknown system: the implementation modes in effect for external ticket systems become Manual
    FOR r_selection IN
        SELECT v.node_id FROM device_specific_config_value v
        WHERE v.config_key = 'rerouteToExternalTicketSystem' AND jsonb_typeof(v.config_value) = 'number'
            AND NOT (v.config_value #>> '{}') IN (SELECT system_id::text FROM tmp_sc_system)
    LOOP
        FOR r_mode IN
            SELECT * FROM (VALUES ('implementationMode', 'reroute_to_external_ticket_system'),
                ('implementationModeForRules', 'rerouteToExternalTicketSystem')) m (config_key, external_value)
        LOOP
            i_ancestor := r_selection.node_id;
            j_effective := NULL;
            i_depth := 0;
            WHILE i_ancestor IS NOT NULL AND j_effective IS NULL AND i_depth < 100 LOOP -- recursion limit, the hierarchy has four levels
                SELECT config_value INTO j_effective FROM device_specific_config_value
                WHERE node_id = i_ancestor AND config_key = r_mode.config_key;
                SELECT parent_id INTO i_ancestor FROM device_specific_config_node WHERE id = i_ancestor;
                i_depth := i_depth + 1;
            END LOOP;
            IF j_effective #>> '{}' = r_mode.external_value THEN
                INSERT INTO device_specific_config_value (node_id, config_key, config_value)
                VALUES (r_selection.node_id, r_mode.config_key, '"Manual"'::jsonb)
                ON CONFLICT (node_id, config_key) DO UPDATE SET config_value = EXCLUDED.config_value;
            END IF;
        END LOOP;
    END LOOP;
    DELETE FROM device_specific_config_value
    WHERE config_key = 'rerouteToExternalTicketSystem' AND (jsonb_typeof(config_value) <> 'number'
        OR NOT (config_value #>> '{}') IN (SELECT system_id::text FROM tmp_sc_system));
    UPDATE device_specific_config_value SET config_value = '"tufinSecureChange"'::jsonb
    WHERE config_key = 'rerouteToExternalTicketSystem';

    INSERT INTO device_specific_config_node (node_type, object_key, display_name, sort_order)
    VALUES ('global', 'global', 'Global', 0)
    ON CONFLICT (node_type, object_key) DO NOTHING;
    SELECT id INTO i_global_node FROM device_specific_config_node WHERE node_type = 'global';

    INSERT INTO device_specific_config_value (node_id, config_key, config_value)
    SELECT i_global_node, k.config_key, k.config_value
    FROM tmp_sc_system s
    CROSS JOIN LATERAL (VALUES ('scUrl', CASE WHEN s.url <> '' THEN to_jsonb(s.url) END),
        ('scCredential', to_jsonb(s.credential_id)),
        ('scLookupRequesterId', CASE WHEN s.lookup_requester_id THEN 'true'::jsonb END)) k (config_key, config_value)
    WHERE s.system_id = i_global_system AND k.config_value IS NOT NULL AND k.config_value <> 'null'::jsonb
    ON CONFLICT (node_id, config_key) DO NOTHING;

    FOR r_route IN
        SELECT r.mgm_id, r.mgm_name, r.dev_typ_id, r.system_id, concat_ws(' ', d.dev_typ_name, d.dev_typ_version) AS device_type_name
        FROM tmp_sc_connection_route r
        JOIN stm_dev_typ d ON d.dev_typ_id = r.dev_typ_id
        WHERE r.system_id <> i_global_system
        ORDER BY r.mgm_id
    LOOP
        i_management_node := NULL;
        FOR r_value IN
            SELECT k.config_key, k.config_value
            FROM tmp_sc_system own
            JOIN tmp_sc_system glob ON glob.system_id = i_global_system
            CROSS JOIN LATERAL (VALUES
                ('scUrl', to_jsonb(own.url), own.url IS DISTINCT FROM glob.url),
                ('scCredential', to_jsonb(own.credential_id), own.credential_id IS NOT NULL AND own.credential_id IS DISTINCT FROM glob.credential_id),
                ('scLookupRequesterId', to_jsonb(own.lookup_requester_id), own.lookup_requester_id IS DISTINCT FROM glob.lookup_requester_id)
            ) k (config_key, config_value, differs)
            WHERE own.system_id = r_route.system_id AND k.differs
            ORDER BY k.config_key
        LOOP
            IF i_management_node IS NULL THEN
                INSERT INTO device_specific_config_node (node_type, object_key, parent_id, display_name)
                VALUES ('device_type', r_route.dev_typ_id::text, i_global_node, r_route.device_type_name)
                ON CONFLICT (node_type, object_key) DO NOTHING;
                SELECT id INTO i_device_type_node FROM device_specific_config_node
                WHERE node_type = 'device_type' AND object_key = r_route.dev_typ_id::text;
                INSERT INTO device_specific_config_node (node_type, object_key, parent_id, display_name)
                VALUES ('management', r_route.mgm_id::text, i_device_type_node, r_route.mgm_name)
                ON CONFLICT (node_type, object_key) DO NOTHING;
                SELECT id INTO i_management_node FROM device_specific_config_node
                WHERE node_type = 'management' AND object_key = r_route.mgm_id::text;
            END IF;
            INSERT INTO device_specific_config_value (node_id, config_key, config_value)
            VALUES (i_management_node, r_value.config_key, r_value.config_value)
            ON CONFLICT (node_id, config_key) DO NOTHING;
        END LOOP;
    END LOOP;

    DROP TABLE tmp_sc_system;
    DROP TABLE tmp_sc_connection_route;
END $$;

-- The ticket system list (extTicketSystems) is replaced by the device specific settings of Tufin SecureChange, which the
-- parts above took over. The list is backed up once without authorization values and removed.
DO $$
DECLARE
    t_systems text;
    j_systems jsonb;
BEGIN
    SELECT config_value INTO t_systems FROM config WHERE config_key = 'extTicketSystems' AND config_user = 0;
    IF t_systems IS NOT NULL THEN
        BEGIN
            j_systems := COALESCE(NULLIF(btrim(t_systems), ''), '[]')::jsonb;
        EXCEPTION WHEN invalid_text_representation THEN
            j_systems := to_jsonb(t_systems);
        END;
        INSERT INTO device_specific_config_backup (version, config_values)
        SELECT '9.8.7 extTicketSystems', CASE WHEN jsonb_typeof(j_systems) = 'array'
            THEN (SELECT COALESCE(jsonb_agg(CASE WHEN jsonb_typeof(s) = 'object' THEN s - 'Authorization' ELSE s END ORDER BY ord), '[]'::jsonb)
                FROM jsonb_array_elements(j_systems) WITH ORDINALITY e(s, ord))
            ELSE j_systems END
        ON CONFLICT (version) DO NOTHING;
    END IF;
    DELETE FROM config WHERE config_key = 'extTicketSystems';
END $$;

-- interval in minutes in which the middleware performs the state actions with event "While set"
insert into config (config_key, config_value, config_user) VALUES ('whileSetActionSleepTime', '10', 0) ON CONFLICT DO NOTHING;

-- config keys a user may set personally (properties marked [UserConfigData] in ConfigData.cs);
-- all other config keys are global only (config_user 0)
CREATE TABLE IF NOT EXISTS config_user_key
(
    config_key VARCHAR NOT NULL,
    PRIMARY KEY (config_key)
);

INSERT INTO config_user_key (config_key) VALUES
    ('maxMessages'),
    ('elementsPerFetch'),
    ('defaultManagementRulebaseView'),
    ('unusedTolerance'),
    ('creationTolerance'),
    ('recertificationDisplayPeriod'),
    ('minCollapseAllDevices'),
    ('messageViewTime'),
    ('overviewDisplayLines'),
    ('modIconify'),
    ('resolveNetworkAreas'),
    ('reportingPersonalPreferredCollapseState')
ON CONFLICT DO NOTHING;

-- personal entries are only kept for keys that can be set personally
DELETE FROM config WHERE config_user <> 0 AND config_key NOT IN (SELECT config_key FROM config_user_key);

-- readers of global settings only read the global rows (config_user 0), as in fworch-views-materialized.sql;
-- the view keeps its column, so the rule owner views that depend on it stay valid
CREATE OR REPLACE VIEW v_rule_ownership_mode AS
    SELECT c.config_value as mode FROM config c
    WHERE c.config_key = 'ruleOwnershipMode' AND c.config_user = 0;
    