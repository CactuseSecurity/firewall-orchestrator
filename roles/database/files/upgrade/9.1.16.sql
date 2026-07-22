CREATE TABLE IF NOT EXISTS request.workflow_configuration
(
    id SERIAL PRIMARY KEY,
    name Varchar NOT NULL UNIQUE,
    description text,
    is_active boolean NOT NULL DEFAULT FALSE
);

CREATE UNIQUE INDEX IF NOT EXISTS request_workflow_configuration_single_active
    ON request.workflow_configuration (is_active) WHERE is_active;

CREATE TABLE IF NOT EXISTS request.state_matrix_phase
(
    id SERIAL PRIMARY KEY,
    name Varchar NOT NULL UNIQUE,
    phase Varchar NOT NULL,
    active boolean NOT NULL DEFAULT FALSE,
    lowest_input_state int NOT NULL,
    lowest_start_state int NOT NULL,
    lowest_end_state int NOT NULL
);

CREATE TABLE IF NOT EXISTS request.workflow_configuration_phase
(
    configuration_id int NOT NULL,
    task_type Varchar NOT NULL,
    phase Varchar NOT NULL,
    phase_matrix_id int NOT NULL,
    PRIMARY KEY (configuration_id, task_type, phase)
);

CREATE TABLE IF NOT EXISTS request.workflow_visibility_group
(
    id SERIAL PRIMARY KEY,
    name Varchar NOT NULL UNIQUE,
    description text
);

CREATE TABLE IF NOT EXISTS request.workflow_visibility_group_member
(
    visibility_group_id int NOT NULL,
    member_dn Varchar NOT NULL,
    PRIMARY KEY (visibility_group_id, member_dn)
);

CREATE TABLE IF NOT EXISTS request.state_matrix_transition_group
(
    id SERIAL PRIMARY KEY,
    name Varchar NOT NULL UNIQUE,
    description text,
    phase Varchar,
    visibility_group_id int,
    exclusive boolean NOT NULL DEFAULT FALSE
);

ALTER TABLE request.state_matrix_transition_group ADD COLUMN IF NOT EXISTS exclusive boolean NOT NULL DEFAULT FALSE;

INSERT INTO config (config_key, config_value, config_user) VALUES ('reqVisibilityBased', 'False', 0) ON CONFLICT DO NOTHING;

CREATE TABLE IF NOT EXISTS request.state_matrix_phase_transition_group
(
    phase_matrix_id int NOT NULL,
    transition_group_id int NOT NULL,
    sort_order int NOT NULL DEFAULT 0,
    PRIMARY KEY (phase_matrix_id, transition_group_id)
);

CREATE TABLE IF NOT EXISTS request.state_matrix_transition
(
    transition_group_id int NOT NULL,
    from_state_id int NOT NULL,
    to_state_id int NOT NULL,
    sort_order int NOT NULL DEFAULT 0,
    PRIMARY KEY (transition_group_id, from_state_id, to_state_id)
);

CREATE TABLE IF NOT EXISTS request.state_matrix_derived_state
(
    phase_matrix_id int NOT NULL,
    from_state_id int NOT NULL,
    derived_state_id int NOT NULL,
    PRIMARY KEY (phase_matrix_id, from_state_id),
    CONSTRAINT state_matrix_derived_state_non_identity CHECK (from_state_id <> derived_state_id)
);

ALTER TABLE request.workflow_configuration_phase DROP CONSTRAINT IF EXISTS request_workflow_configuration_phase_configuration_foreign_key;
ALTER TABLE request.workflow_configuration_phase ADD CONSTRAINT request_workflow_configuration_phase_configuration_foreign_key FOREIGN KEY (configuration_id) REFERENCES request.workflow_configuration(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.workflow_configuration_phase DROP CONSTRAINT IF EXISTS request_workflow_configuration_phase_phase_foreign_key;
ALTER TABLE request.workflow_configuration_phase ADD CONSTRAINT request_workflow_configuration_phase_phase_foreign_key FOREIGN KEY (phase_matrix_id) REFERENCES request.state_matrix_phase(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.workflow_visibility_group_member DROP CONSTRAINT IF EXISTS request_workflow_visibility_group_member_group_foreign_key;
ALTER TABLE request.workflow_visibility_group_member ADD CONSTRAINT request_workflow_visibility_group_member_group_foreign_key FOREIGN KEY (visibility_group_id) REFERENCES request.workflow_visibility_group(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.state_matrix_transition_group DROP CONSTRAINT IF EXISTS request_state_matrix_transition_group_visibility_group_foreign_key;
ALTER TABLE request.state_matrix_transition_group ADD CONSTRAINT request_state_matrix_transition_group_visibility_group_foreign_key FOREIGN KEY (visibility_group_id) REFERENCES request.workflow_visibility_group(id) ON UPDATE RESTRICT ON DELETE SET NULL;

ALTER TABLE request.state_matrix_phase_transition_group DROP CONSTRAINT IF EXISTS request_state_matrix_phase_transition_group_phase_foreign_key;
ALTER TABLE request.state_matrix_phase_transition_group ADD CONSTRAINT request_state_matrix_phase_transition_group_phase_foreign_key FOREIGN KEY (phase_matrix_id) REFERENCES request.state_matrix_phase(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.state_matrix_phase_transition_group DROP CONSTRAINT IF EXISTS request_state_matrix_phase_transition_group_group_foreign_key;
ALTER TABLE request.state_matrix_phase_transition_group ADD CONSTRAINT request_state_matrix_phase_transition_group_group_foreign_key FOREIGN KEY (transition_group_id) REFERENCES request.state_matrix_transition_group(id) ON UPDATE RESTRICT ON DELETE CASCADE;

ALTER TABLE request.state_matrix_transition DROP CONSTRAINT IF EXISTS request_state_matrix_transition_group_foreign_key;
ALTER TABLE request.state_matrix_transition ADD CONSTRAINT request_state_matrix_transition_group_foreign_key FOREIGN KEY (transition_group_id) REFERENCES request.state_matrix_transition_group(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.state_matrix_transition DROP CONSTRAINT IF EXISTS request_state_matrix_transition_from_state_foreign_key;
ALTER TABLE request.state_matrix_transition ADD CONSTRAINT request_state_matrix_transition_from_state_foreign_key FOREIGN KEY (from_state_id) REFERENCES request.state(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.state_matrix_transition DROP CONSTRAINT IF EXISTS request_state_matrix_transition_to_state_foreign_key;
ALTER TABLE request.state_matrix_transition ADD CONSTRAINT request_state_matrix_transition_to_state_foreign_key FOREIGN KEY (to_state_id) REFERENCES request.state(id) ON UPDATE RESTRICT ON DELETE CASCADE;

ALTER TABLE request.state_matrix_derived_state DROP CONSTRAINT IF EXISTS request_state_matrix_derived_state_phase_foreign_key;
ALTER TABLE request.state_matrix_derived_state ADD CONSTRAINT request_state_matrix_derived_state_phase_foreign_key FOREIGN KEY (phase_matrix_id) REFERENCES request.state_matrix_phase(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.state_matrix_derived_state DROP CONSTRAINT IF EXISTS request_state_matrix_derived_state_from_state_foreign_key;
ALTER TABLE request.state_matrix_derived_state ADD CONSTRAINT request_state_matrix_derived_state_from_state_foreign_key FOREIGN KEY (from_state_id) REFERENCES request.state(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.state_matrix_derived_state DROP CONSTRAINT IF EXISTS request_state_matrix_derived_state_derived_state_foreign_key;
ALTER TABLE request.state_matrix_derived_state ADD CONSTRAINT request_state_matrix_derived_state_derived_state_foreign_key FOREIGN KEY (derived_state_id) REFERENCES request.state(id) ON UPDATE RESTRICT ON DELETE CASCADE;

-- Legacy config data is copied only while the normalized configuration store is empty.
-- This prevents repeated upgrades from restoring transitions or mappings deleted by users.
DO $state_matrix_migration$
BEGIN
IF EXISTS (SELECT 1 FROM request.workflow_configuration) THEN
    RETURN;
END IF;

INSERT INTO request.workflow_configuration (name, description, is_active)
VALUES
    ('current', 'Migrated workflow state matrix configuration', TRUE),
    ('installation-default', 'Workflow configuration proposal delivered with the installation', FALSE)
ON CONFLICT (name) DO NOTHING;

DROP TABLE IF EXISTS pg_temp.tmp_state_matrix_key;
CREATE TEMP TABLE tmp_state_matrix_key
(
    config_key Varchar,
    configuration_name Varchar,
    task_type Varchar
);

INSERT INTO tmp_state_matrix_key (config_key, configuration_name, task_type)
VALUES
    ('reqMasterStateMatrix', 'current', 'master'),
    ('reqGenStateMatrix', 'current', 'generic'),
    ('reqAccStateMatrix', 'current', 'access'),
    ('reqRulDelStateMatrix', 'current', 'rule_delete'),
    ('reqRulModStateMatrix', 'current', 'rule_modify'),
    ('reqGrpCreStateMatrix', 'current', 'group_create'),
    ('reqGrpModStateMatrix', 'current', 'group_modify'),
    ('reqGrpDelStateMatrix', 'current', 'group_delete'),
    ('reqNewIntStateMatrix', 'current', 'new_interface'),
    ('reqMasterStateMatrixDefault', 'installation-default', 'master'),
    ('reqGenStateMatrixDefault', 'installation-default', 'generic'),
    ('reqAccStateMatrixDefault', 'installation-default', 'access'),
    ('reqRulDelStateMatrixDefault', 'installation-default', 'rule_delete'),
    ('reqRulModStateMatrixDefault', 'installation-default', 'rule_modify'),
    ('reqGrpCreStateMatrixDefault', 'installation-default', 'group_create'),
    ('reqGrpModStateMatrixDefault', 'installation-default', 'group_modify'),
    ('reqGrpDelStateMatrixDefault', 'installation-default', 'group_delete'),
    ('reqNewIntStateMatrixDefault', 'installation-default', 'new_interface');

WITH phase_data AS (
    SELECT
        matrix_key.configuration_name,
        matrix_key.task_type,
        phase.key AS phase,
        phase.value AS phase_config,
        matrix_key.configuration_name || '_' || matrix_key.task_type || '_' || phase.key AS phase_name
    FROM tmp_state_matrix_key matrix_key
    JOIN config ON config.config_key = matrix_key.config_key AND config.config_user = 0
    CROSS JOIN LATERAL jsonb_each((config.config_value::jsonb)->'config_value') AS phase
)
INSERT INTO request.state_matrix_phase (name, phase, active, lowest_input_state, lowest_start_state, lowest_end_state)
SELECT
    phase_name,
    phase,
    COALESCE((phase_config->>'active')::boolean, FALSE),
    (phase_config->>'lowest_input_state')::int,
    (phase_config->>'lowest_start_state')::int,
    (phase_config->>'lowest_end_state')::int
FROM phase_data
ON CONFLICT (name) DO NOTHING;

WITH phase_data AS (
    SELECT
        matrix_key.configuration_name,
        matrix_key.task_type,
        phase.key AS phase,
        matrix_key.configuration_name || '_' || matrix_key.task_type || '_' || phase.key AS phase_name
    FROM tmp_state_matrix_key matrix_key
    JOIN config ON config.config_key = matrix_key.config_key AND config.config_user = 0
    CROSS JOIN LATERAL jsonb_each((config.config_value::jsonb)->'config_value') AS phase
)
INSERT INTO request.workflow_configuration_phase (configuration_id, task_type, phase, phase_matrix_id)
SELECT configuration.id, phase_data.task_type, phase_data.phase, matrix_phase.id
FROM phase_data
JOIN request.workflow_configuration configuration ON configuration.name = phase_data.configuration_name
JOIN request.state_matrix_phase matrix_phase ON matrix_phase.name = phase_data.phase_name
ON CONFLICT (configuration_id, task_type, phase) DO NOTHING;

WITH phase_data AS (
    SELECT
        matrix_key.configuration_name,
        matrix_key.task_type,
        phase.key AS phase,
        matrix_phase.id AS phase_matrix_id,
        matrix_key.configuration_name || '_' || matrix_key.task_type || '_' || phase.key || '_transitions' AS group_name
    FROM tmp_state_matrix_key matrix_key
    JOIN config ON config.config_key = matrix_key.config_key AND config.config_user = 0
    CROSS JOIN LATERAL jsonb_each((config.config_value::jsonb)->'config_value') AS phase
    JOIN request.state_matrix_phase matrix_phase ON matrix_phase.name = matrix_key.configuration_name || '_' || matrix_key.task_type || '_' || phase.key
)
INSERT INTO request.state_matrix_transition_group (name, description, phase, visibility_group_id)
SELECT group_name, 'Migrated transitions for ' || group_name, phase, NULL
FROM phase_data
ON CONFLICT (name) DO NOTHING;

WITH phase_data AS (
    SELECT
        matrix_key.configuration_name,
        matrix_key.task_type,
        phase.key AS phase,
        matrix_phase.id AS phase_matrix_id,
        matrix_key.configuration_name || '_' || matrix_key.task_type || '_' || phase.key || '_transitions' AS group_name
    FROM tmp_state_matrix_key matrix_key
    JOIN config ON config.config_key = matrix_key.config_key AND config.config_user = 0
    CROSS JOIN LATERAL jsonb_each((config.config_value::jsonb)->'config_value') AS phase
    JOIN request.state_matrix_phase matrix_phase ON matrix_phase.name = matrix_key.configuration_name || '_' || matrix_key.task_type || '_' || phase.key
)
INSERT INTO request.state_matrix_phase_transition_group (phase_matrix_id, transition_group_id, sort_order)
SELECT phase_data.phase_matrix_id, transition_group.id, 0
FROM phase_data
JOIN request.state_matrix_transition_group transition_group ON transition_group.name = phase_data.group_name
ON CONFLICT (phase_matrix_id, transition_group_id) DO NOTHING;

WITH phase_data AS (
    SELECT
        phase.value AS phase_config,
        transition_group.id AS transition_group_id
    FROM tmp_state_matrix_key matrix_key
    JOIN config ON config.config_key = matrix_key.config_key AND config.config_user = 0
    CROSS JOIN LATERAL jsonb_each((config.config_value::jsonb)->'config_value') AS phase
    JOIN request.state_matrix_transition_group transition_group ON transition_group.name = matrix_key.configuration_name || '_' || matrix_key.task_type || '_' || phase.key || '_transitions'
),
transition_data AS (
    SELECT
        transition_group_id,
        transition.key::int AS from_state_id,
        target.value::int AS to_state_id,
        target.ordinality::int AS sort_order
    FROM phase_data
    CROSS JOIN LATERAL jsonb_each(phase_config->'matrix') AS transition
    CROSS JOIN LATERAL jsonb_array_elements_text(transition.value) WITH ORDINALITY AS target(value, ordinality)
)
INSERT INTO request.state_matrix_transition (transition_group_id, from_state_id, to_state_id, sort_order)
SELECT transition_group_id, from_state_id, to_state_id, sort_order
FROM transition_data
JOIN request.state from_state ON from_state.id = transition_data.from_state_id
JOIN request.state to_state ON to_state.id = transition_data.to_state_id
ON CONFLICT (transition_group_id, from_state_id, to_state_id) DO NOTHING;

WITH phase_data AS (
    SELECT
        phase.value AS phase_config,
        matrix_phase.id AS phase_matrix_id
    FROM tmp_state_matrix_key matrix_key
    JOIN config ON config.config_key = matrix_key.config_key AND config.config_user = 0
    CROSS JOIN LATERAL jsonb_each((config.config_value::jsonb)->'config_value') AS phase
    JOIN request.state_matrix_phase matrix_phase ON matrix_phase.name = matrix_key.configuration_name || '_' || matrix_key.task_type || '_' || phase.key
),
derived_state_data AS (
    SELECT
        phase_matrix_id,
        derived_state.key::int AS from_state_id,
        derived_state.value::int AS derived_state_id
    FROM phase_data
    CROSS JOIN LATERAL jsonb_each_text(phase_config->'derived_states') AS derived_state
    WHERE derived_state.key::int <> derived_state.value::int
)
INSERT INTO request.state_matrix_derived_state (phase_matrix_id, from_state_id, derived_state_id)
SELECT phase_matrix_id, from_state_id, derived_state_id
FROM derived_state_data
JOIN request.state from_state ON from_state.id = derived_state_data.from_state_id
JOIN request.state derived_state ON derived_state.id = derived_state_data.derived_state_id
ON CONFLICT (phase_matrix_id, from_state_id) DO NOTHING;

DROP TABLE IF EXISTS pg_temp.tmp_state_matrix_key;
END;
$state_matrix_migration$;
