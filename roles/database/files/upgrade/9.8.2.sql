-- Implementation tasks are created according to the device specific settings autoPlanImplementation and
-- autoPlanImplementationForRules instead of depending on the planning phase. The device selection strategies of
-- the former option reqAutoCreateImplTasks become path analysis algorithms, the device input in the request a
-- setting of its own. The settings are not derived from the planning phases: an administrator sets them.

INSERT INTO path_analysis_algorithm (id, name) VALUES
    (3, 'For each device'),
    (4, 'First device (if available)')
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
BEGIN
    SELECT config_value INTO t_old_option FROM config WHERE config_key = 'reqAutoCreateImplTasks' AND config_user = 0;
    IF t_old_option IS NULL THEN
        RETURN;
    END IF;

    -- The request form showed the device field only without planning phase of access tasks; with an active planning
    -- phase the former option had no effect on the form, so the device input stays hidden there.
    SELECT COALESCE(bool_or(p.active), FALSE) INTO b_access_planning
    FROM request.workflow_configuration c
    JOIN request.workflow_configuration_phase wp ON wp.configuration_id = c.id
    JOIN request.state_matrix_phase p ON p.id = wp.phase_matrix_id
    WHERE c.is_active AND wp.phase = 'planning' AND wp.task_type = 'access';

    INSERT INTO config (config_key, config_value, config_user)
    SELECT 'reqDeviceInput',
           CASE WHEN NOT b_access_planning AND config_value IN ('enterInReqTask', 'oneTaskForAllDevices') THEN config_value ELSE 'never' END,
           config_user
    FROM config WHERE config_key = 'reqAutoCreateImplTasks'
    ON CONFLICT (config_key, config_user) DO NOTHING;

    -- the device selection strategies are path analysis algorithms now; after path analysis keeps the algorithm
    IF t_old_option IN ('forEachDevice', 'onlyForOneDevice') THEN
        INSERT INTO config (config_key, config_value, config_user)
        VALUES ('pathAnalysisAlgorithm', CASE t_old_option WHEN 'forEachDevice' THEN '3' ELSE '4' END, 0)
        ON CONFLICT (config_key, config_user) DO UPDATE SET config_value = EXCLUDED.config_value;
    END IF;

    DELETE FROM config WHERE config_key = 'reqAutoCreateImplTasks';
END $$;

-- Implementation tasks are only created automatically by the state action "Create implementation tasks". It is
-- provided once, but not attached to a state: an administrator decides where it runs, e.g. at the state approved.
INSERT INTO request.action (name, action_type, scope, task_type, phase, event, button_text, external_parameters)
SELECT 'Create implementation tasks', 'CreateImplTasks', 'Ticket', '', '', 'OnSet', '', ''
WHERE NOT EXISTS (SELECT 1 FROM request.action WHERE action_type = 'CreateImplTasks');
