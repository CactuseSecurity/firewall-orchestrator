-- Implementation tasks are created according to the device specific settings autoPlanImplementation and
-- autoPlanImplementationForRules instead of depending on the planning phase. The device selection strategies of
-- the former option reqAutoCreateImplTasks become path analysis algorithms, the device input in the request a
-- setting of its own.

INSERT INTO path_analysis_algorithm (id, name) VALUES
    (3, 'For each device'),
    (4, 'Only if device available')
ON CONFLICT DO NOTHING;

-- The planning settings are renamed after their new meaning. An already stored new key is kept.
INSERT INTO device_specific_config_value (node_id, config_key, config_value)
SELECT node_id,
       CASE config_key
           WHEN 'planImplementation' THEN 'autoPlanImplementation'
           WHEN 'planImplementationOverrideForRules' THEN 'autoPlanImplementationForRules'
       END, config_value
FROM device_specific_config_value
WHERE config_key IN ('planImplementation', 'planImplementationOverrideForRules')
ON CONFLICT (node_id, config_key) DO NOTHING;
DELETE FROM device_specific_config_value WHERE config_key IN ('planImplementation', 'planImplementationOverrideForRules');

-- Translate the former option once; afterwards it is removed, so a repeated upgrade changes nothing.
DO $$
DECLARE
    t_old_option varchar;
    b_access_planning boolean;
    b_all_planning boolean;
    i_global_node bigint;
BEGIN
    SELECT config_value INTO t_old_option FROM config WHERE config_key = 'reqAutoCreateImplTasks' AND config_user = 0;
    IF t_old_option IS NULL THEN
        RETURN;
    END IF;

    INSERT INTO config (config_key, config_value, config_user)
    SELECT 'reqDeviceInput', CASE WHEN config_value IN ('enterInReqTask', 'oneTaskForAllDevices') THEN config_value ELSE 'never' END, config_user
    FROM config WHERE config_key = 'reqAutoCreateImplTasks'
    ON CONFLICT (config_key, config_user) DO NOTHING;

    -- the device selection strategies are path analysis algorithms now; after path analysis keeps the algorithm
    IF t_old_option IN ('forEachDevice', 'onlyForOneDevice') THEN
        INSERT INTO config (config_key, config_value, config_user)
        VALUES ('pathAnalysisAlgorithm', CASE t_old_option WHEN 'forEachDevice' THEN '3' ELSE '4' END, 0)
        ON CONFLICT (config_key, config_user) DO UPDATE SET config_value = EXCLUDED.config_value;
    END IF;

    -- Implementation tasks were created automatically only for task types without active planning phase, and for
    -- access tasks only if the option was not never.
    SELECT COALESCE(bool_or(p.active), FALSE) INTO b_access_planning
    FROM request.workflow_configuration c
    JOIN request.workflow_configuration_phase wp ON wp.configuration_id = c.id
    JOIN request.state_matrix_phase p ON p.id = wp.phase_matrix_id
    WHERE c.is_active AND wp.phase = 'planning' AND wp.task_type = 'access';

    SELECT COALESCE(bool_and(p.active), FALSE) INTO b_all_planning
    FROM request.workflow_configuration c
    JOIN request.workflow_configuration_phase wp ON wp.configuration_id = c.id
    JOIN request.state_matrix_phase p ON p.id = wp.phase_matrix_id
    WHERE c.is_active AND wp.phase = 'planning' AND wp.task_type <> 'master';

    INSERT INTO device_specific_config_node (node_type, object_key, display_name, sort_order)
    VALUES ('global', 'global', 'Global', 0)
    ON CONFLICT (node_type, object_key) DO NOTHING;
    SELECT id INTO i_global_node FROM device_specific_config_node WHERE node_type = 'global';

    IF t_old_option <> 'never' AND NOT b_access_planning THEN
        INSERT INTO device_specific_config_value (node_id, config_key, config_value)
        VALUES (i_global_node, 'autoPlanImplementationForRules', 'true'::jsonb)
        ON CONFLICT (node_id, config_key) DO NOTHING;
    END IF;
    IF b_all_planning THEN
        INSERT INTO device_specific_config_value (node_id, config_key, config_value)
        VALUES (i_global_node, 'autoPlanImplementation', 'false'::jsonb)
        ON CONFLICT (node_id, config_key) DO NOTHING;
    END IF;

    DELETE FROM config WHERE config_key = 'reqAutoCreateImplTasks';
END $$;
