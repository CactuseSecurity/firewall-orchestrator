-- New workflow task types object_create and object_modify (single network object or service without group).
-- Every existing workflow configuration gets its own copy of the group_create phase matrices for both new
-- task types, so that later changes to the group matrices do not affect the object task types and vice versa.
-- A configuration that already has a mapping for a new task type is left untouched, so repeated upgrades
-- neither duplicate the copies nor restore mappings an admin removed later.
DO $object_task_type_matrices$
DECLARE
    v_configuration RECORD;
    v_source_phase RECORD;
    v_source_group RECORD;
    v_task_type Varchar;
    v_phase_name Varchar;
    v_group_name Varchar;
    v_new_phase_id int;
    v_new_group_id int;
BEGIN
    FOREACH v_task_type IN ARRAY ARRAY['object_create', 'object_modify']
    LOOP
        FOR v_configuration IN
            SELECT configuration.id, configuration.name
            FROM request.workflow_configuration configuration
            WHERE NOT EXISTS (
                SELECT 1 FROM request.workflow_configuration_phase mapping
                WHERE mapping.configuration_id = configuration.id AND mapping.task_type = v_task_type)
        LOOP
            FOR v_source_phase IN
                SELECT mapping.phase, matrix_phase.id, matrix_phase.phase AS matrix_phase, matrix_phase.active,
                    matrix_phase.lowest_input_state, matrix_phase.lowest_start_state, matrix_phase.lowest_end_state,
                    matrix_phase.phase_visibility_mode
                FROM request.workflow_configuration_phase mapping
                JOIN request.state_matrix_phase matrix_phase ON matrix_phase.id = mapping.phase_matrix_id
                WHERE mapping.configuration_id = v_configuration.id AND mapping.task_type = 'group_create'
            LOOP
                v_phase_name := v_configuration.name || '_' || v_task_type || '_' || v_source_phase.phase;
                IF EXISTS (SELECT 1 FROM request.state_matrix_phase WHERE name = v_phase_name) THEN
                    v_phase_name := v_phase_name || '_' || v_configuration.id;
                END IF;

                INSERT INTO request.state_matrix_phase (name, phase, active, lowest_input_state, lowest_start_state,
                    lowest_end_state, phase_visibility_mode)
                VALUES (v_phase_name, v_source_phase.matrix_phase, v_source_phase.active, v_source_phase.lowest_input_state,
                    v_source_phase.lowest_start_state, v_source_phase.lowest_end_state, v_source_phase.phase_visibility_mode)
                RETURNING id INTO v_new_phase_id;

                INSERT INTO request.workflow_configuration_phase (configuration_id, task_type, phase, phase_matrix_id)
                VALUES (v_configuration.id, v_task_type, v_source_phase.phase, v_new_phase_id)
                ON CONFLICT (configuration_id, task_type, phase) DO NOTHING;

                INSERT INTO request.state_matrix_derived_state (phase_matrix_id, from_state_id, derived_state_id)
                SELECT v_new_phase_id, derived_state.from_state_id, derived_state.derived_state_id
                FROM request.state_matrix_derived_state derived_state
                WHERE derived_state.phase_matrix_id = v_source_phase.id
                ON CONFLICT (phase_matrix_id, from_state_id) DO NOTHING;

                FOR v_source_group IN
                    SELECT transition_group.id, transition_group.name, transition_group.description, transition_group.phase,
                        transition_group.visibility_group_id, transition_group.exclusive, phase_group.sort_order
                    FROM request.state_matrix_phase_transition_group phase_group
                    JOIN request.state_matrix_transition_group transition_group ON transition_group.id = phase_group.transition_group_id
                    WHERE phase_group.phase_matrix_id = v_source_phase.id
                LOOP
                    v_group_name := v_phase_name || '_transitions_' || v_source_group.id;

                    INSERT INTO request.state_matrix_transition_group (name, description, phase, visibility_group_id, exclusive)
                    VALUES (v_group_name, v_source_group.description, v_source_group.phase,
                        v_source_group.visibility_group_id, v_source_group.exclusive)
                    ON CONFLICT (name) DO NOTHING;

                    SELECT id INTO v_new_group_id FROM request.state_matrix_transition_group WHERE name = v_group_name;

                    INSERT INTO request.state_matrix_phase_transition_group (phase_matrix_id, transition_group_id, sort_order)
                    VALUES (v_new_phase_id, v_new_group_id, v_source_group.sort_order)
                    ON CONFLICT (phase_matrix_id, transition_group_id) DO NOTHING;

                    INSERT INTO request.state_matrix_transition (transition_group_id, from_state_id, to_state_id, sort_order)
                    SELECT v_new_group_id, transition.from_state_id, transition.to_state_id, transition.sort_order
                    FROM request.state_matrix_transition transition
                    WHERE transition.transition_group_id = v_source_group.id
                    ON CONFLICT (transition_group_id, from_state_id, to_state_id) DO NOTHING;
                END LOOP;
            END LOOP;
        END LOOP;
    END LOOP;
END;
$object_task_type_matrices$;
