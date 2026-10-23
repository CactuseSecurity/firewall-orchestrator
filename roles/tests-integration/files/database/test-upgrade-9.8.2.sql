-- Test of upgrade/9.8.2.sql on the installed database. The test task inserts the upgrade file at the two run-upgrade
-- markers below, which also shows that the upgrade can be repeated. The transaction is rolled back.
-- This probes the new path analysis algorithms, the rename of the planning settings, the translation of the former
-- option reqAutoCreateImplTasks (for each device, access planning phase inactive), the creation of the action
-- "Create implementation tasks", which is not attached to any state, and the repeat execution.
\set ON_ERROR_STOP 1
BEGIN;
DELETE FROM config WHERE config_key IN ('reqDeviceInput', 'reqAutoCreateImplTasks');
INSERT INTO config (config_key, config_value, config_user) VALUES ('reqAutoCreateImplTasks', 'forEachDevice', 0);
INSERT INTO config (config_key, config_value, config_user) VALUES ('pathAnalysisAlgorithm', '1', 0)
    ON CONFLICT (config_key, config_user) DO UPDATE SET config_value = EXCLUDED.config_value;
INSERT INTO device_specific_config_node (node_type, object_key, display_name, sort_order)
    VALUES ('global', 'global', 'Global', 0) ON CONFLICT (node_type, object_key) DO NOTHING;
DELETE FROM device_specific_config_value WHERE node_id = (SELECT id FROM device_specific_config_node WHERE node_type = 'global')
    AND config_key IN ('autoPlanImplementation', 'autoPlanImplementationForRules', 'planImplementation', 'planImplementationOverrideForRules');
INSERT INTO device_specific_config_value (node_id, config_key, config_value)
    SELECT id, 'planImplementation', 'false'::jsonb FROM device_specific_config_node WHERE node_type = 'global';
-- the action is created by the upgrade, so the one of the installation is removed
DELETE FROM request.action WHERE action_type = 'CreateImplTasks';
-- the default workflow configuration has no active planning phase for access tasks
UPDATE request.state_matrix_phase SET active = FALSE WHERE id IN (
    SELECT wp.phase_matrix_id FROM request.workflow_configuration c
    JOIN request.workflow_configuration_phase wp ON wp.configuration_id = c.id
    WHERE c.is_active AND wp.phase = 'planning' AND wp.task_type = 'access');

-- @run-upgrade-9.8.2

DO $$
DECLARE
    i_global bigint;
BEGIN
    SELECT id INTO i_global FROM device_specific_config_node WHERE node_type = 'global';
    IF (SELECT count(*) FROM path_analysis_algorithm WHERE id IN (3, 4)) <> 2 THEN
        RAISE EXCEPTION 'upgrade 9.8.2: path analysis algorithms 3 and 4 missing';
    END IF;
    IF EXISTS (SELECT 1 FROM config WHERE config_key = 'reqAutoCreateImplTasks') THEN
        RAISE EXCEPTION 'upgrade 9.8.2: reqAutoCreateImplTasks not removed';
    END IF;
    IF (SELECT config_value FROM config WHERE config_key = 'reqDeviceInput' AND config_user = 0) IS DISTINCT FROM 'never' THEN
        RAISE EXCEPTION 'upgrade 9.8.2: forEachDevice must become reqDeviceInput never';
    END IF;
    IF (SELECT config_value FROM config WHERE config_key = 'pathAnalysisAlgorithm' AND config_user = 0) IS DISTINCT FROM '3' THEN
        RAISE EXCEPTION 'upgrade 9.8.2: forEachDevice must become path analysis algorithm 3';
    END IF;
    IF EXISTS (SELECT 1 FROM device_specific_config_value
        WHERE config_key IN ('planImplementation', 'planImplementationOverrideForRules')) THEN
        RAISE EXCEPTION 'upgrade 9.8.2: old planning keys not removed';
    END IF;
    IF (SELECT config_value FROM device_specific_config_value WHERE node_id = i_global AND config_key = 'autoPlanImplementation')
        IS DISTINCT FROM 'false'::jsonb THEN
        RAISE EXCEPTION 'upgrade 9.8.2: planImplementation must be renamed to autoPlanImplementation keeping its value';
    END IF;
    IF (SELECT config_value FROM device_specific_config_value WHERE node_id = i_global AND config_key = 'autoPlanImplementationForRules')
        IS DISTINCT FROM 'true'::jsonb THEN
        RAISE EXCEPTION 'upgrade 9.8.2: automatically created access tasks must keep rules planned automatically';
    END IF;
    IF (SELECT count(*) FROM request.action WHERE action_type = 'CreateImplTasks' AND scope = 'Ticket' AND event = 'OnSet') <> 1 THEN
        RAISE EXCEPTION 'upgrade 9.8.2: the action creating implementation tasks must be provided';
    END IF;
    IF EXISTS (SELECT 1 FROM request.state_action sa JOIN request.action a ON a.id = sa.action_id
        WHERE a.action_type = 'CreateImplTasks') THEN
        RAISE EXCEPTION 'upgrade 9.8.2: the action creating implementation tasks must not be attached automatically';
    END IF;
END $$;

CREATE TEMP TABLE config_after_first_run AS SELECT config_key, config_value, config_user FROM config;
CREATE TEMP TABLE values_after_first_run AS SELECT node_id, config_key, config_value FROM device_specific_config_value;

-- @run-upgrade-9.8.2

DO $$
BEGIN
    IF EXISTS (
        (SELECT config_key, config_value, config_user FROM config EXCEPT SELECT * FROM config_after_first_run)
        UNION ALL
        (SELECT * FROM config_after_first_run EXCEPT SELECT config_key, config_value, config_user FROM config)
    ) OR EXISTS (
        (SELECT node_id, config_key, config_value FROM device_specific_config_value EXCEPT SELECT * FROM values_after_first_run)
        UNION ALL
        (SELECT * FROM values_after_first_run EXCEPT SELECT node_id, config_key, config_value FROM device_specific_config_value)
    ) THEN
        RAISE EXCEPTION 'upgrade 9.8.2: rerunning the migration changed the configuration';
    END IF;
    IF (SELECT count(*) FROM request.action WHERE action_type = 'CreateImplTasks') <> 1 THEN
        RAISE EXCEPTION 'upgrade 9.8.2: rerunning the migration duplicated the action creating implementation tasks';
    END IF;
END $$;
ROLLBACK;
