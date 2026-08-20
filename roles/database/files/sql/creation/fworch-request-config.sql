DROP TABLE IF EXISTS pg_temp.tmp_state_matrix_seed;
CREATE TEMP TABLE tmp_state_matrix_seed
(
    config_key Varchar PRIMARY KEY,
    config_value jsonb NOT NULL
);
INSERT INTO tmp_state_matrix_seed (config_key, config_value) VALUES ('reqMasterStateMatrixDefault', '{"config_value":{"request":{"matrix":{"0":[0,49,620],"49":[49,620],"620":[620]},"derived_states":{"0":0,"49":49,"620":620},"lowest_input_state":0,"lowest_start_state":49,"lowest_end_state":49,"active":true},"approval":{"matrix":{"49":[60],"60":[60,99,610],"99":[99],"610":[610]},"derived_states":{"49":49,"60":60,"99":99,"610":610},"lowest_input_state":49,"lowest_start_state":60,"lowest_end_state":99,"active":true},"planning":{"matrix":{"99":[110],"110":[110,120,130,149],"120":[120,110,130,149],"130":[130,110,120,149,610],"149":[149],"610":[610]},"derived_states":{"99":99,"110":110,"120":110,"130":110,"149":149,"610":610},"lowest_input_state":99,"lowest_start_state":110,"lowest_end_state":149,"active":false},"verification":{"matrix":{"149":[160],"160":[160,199,610],"199":[199],"610":[610]},"derived_states":{"149":149,"160":160,"199":199,"610":610},"lowest_input_state":149,"lowest_start_state":160,"lowest_end_state":199,"active":false},"implementation":{"matrix":{"99":[210],"210":[210,220,249],"220":[220,210,249,610],"249":[249],"610":[610]},"derived_states":{"99":99,"210":210,"220":210,"249":249,"610":610},"lowest_input_state":99,"lowest_start_state":210,"lowest_end_state":249,"active":true},"review":{"matrix":{"249":[260],"260":[260,270,299],"270":[210,270,260,299,610],"299":[299],"610":[610]},"derived_states":{"249":249,"260":260,"270":260,"299":299,"610":610},"lowest_input_state":249,"lowest_start_state":260,"lowest_end_state":299,"active":false},"recertification":{"matrix":{"299":[310],"310":[310,349,400],"349":[349],"400":[400]},"derived_states":{"299":299,"310":310,"349":349,"400":400},"lowest_input_state":299,"lowest_start_state":310,"lowest_end_state":349,"active":false}}}');
INSERT INTO tmp_state_matrix_seed (config_key, config_value) VALUES ('reqGenStateMatrixDefault', '{"config_value":{"request":{"matrix":{"0":[0,49,620],"49":[49,620],"620":[620]},"derived_states":{"0":0,"49":49,"620":620},"lowest_input_state":0,"lowest_start_state":49,"lowest_end_state":49,"active":true},"approval":{"matrix":{"49":[60],"60":[60,99,610],"99":[99],"610":[610]},"derived_states":{"49":49,"60":60,"99":99,"610":610},"lowest_input_state":49,"lowest_start_state":60,"lowest_end_state":99,"active":true},"planning":{"matrix":{"99":[110],"110":[110,120,130,149],"120":[120,110,130,149],"130":[130,110,120,149,610],"149":[149],"610":[610]},"derived_states":{"99":99,"110":110,"120":110,"130":110,"149":149,"610":610},"lowest_input_state":99,"lowest_start_state":110,"lowest_end_state":149,"active":false},"verification":{"matrix":{"149":[160],"160":[160,199,610],"199":[199],"610":[610]},"derived_states":{"149":149,"160":160,"199":199,"610":610},"lowest_input_state":149,"lowest_start_state":160,"lowest_end_state":199,"active":false},"implementation":{"matrix":{"99":[210],"210":[210,220,249],"220":[220,210,249,610],"249":[249],"610":[610]},"derived_states":{"99":99,"210":210,"220":210,"249":249,"610":610},"lowest_input_state":99,"lowest_start_state":210,"lowest_end_state":249,"active":true},"review":{"matrix":{"249":[260],"260":[260,270,299],"270":[210,270,260,299,610],"299":[299],"610":[610]},"derived_states":{"249":249,"260":260,"270":260,"299":299,"610":610},"lowest_input_state":249,"lowest_start_state":260,"lowest_end_state":299,"active":false},"recertification":{"matrix":{"299":[310],"310":[310,349,400],"349":[349],"400":[400]},"derived_states":{"299":299,"310":310,"349":349,"400":400},"lowest_input_state":299,"lowest_start_state":310,"lowest_end_state":349,"active":false}}}');
INSERT INTO tmp_state_matrix_seed (config_key, config_value) VALUES ('reqAccStateMatrixDefault', '{"config_value":{"request":{"matrix":{"0":[0,49,620],"49":[49,620],"620":[620]},"derived_states":{"0":0,"49":49,"620":620},"lowest_input_state":0,"lowest_start_state":49,"lowest_end_state":49,"active":true},"approval":{"matrix":{"49":[60],"60":[60,99,610],"99":[99],"610":[610]},"derived_states":{"49":49,"60":60,"99":99,"610":610},"lowest_input_state":49,"lowest_start_state":60,"lowest_end_state":99,"active":true},"planning":{"matrix":{"99":[110],"110":[110,120,130,149],"120":[120,110,130,149],"130":[130,110,120,149,610],"149":[149],"610":[610]},"derived_states":{"99":99,"110":110,"120":110,"130":110,"149":149,"610":610},"lowest_input_state":99,"lowest_start_state":110,"lowest_end_state":149,"active":false},"verification":{"matrix":{"149":[160],"160":[160,199,610],"199":[199],"610":[610]},"derived_states":{"149":149,"160":160,"199":199,"610":610},"lowest_input_state":149,"lowest_start_state":160,"lowest_end_state":199,"active":false},"implementation":{"matrix":{"99":[210],"210":[210,220,249],"220":[220,210,249,610],"249":[249],"610":[610]},"derived_states":{"99":99,"210":210,"220":210,"249":249,"610":610},"lowest_input_state":99,"lowest_start_state":210,"lowest_end_state":249,"active":true},"review":{"matrix":{"249":[260],"260":[260,270,299],"270":[210,270,260,299,610],"299":[299],"610":[610]},"derived_states":{"249":249,"260":260,"270":260,"299":299,"610":610},"lowest_input_state":249,"lowest_start_state":260,"lowest_end_state":299,"active":false},"recertification":{"matrix":{"299":[310],"310":[310,349,400],"349":[349],"400":[400]},"derived_states":{"299":299,"310":310,"349":349,"400":400},"lowest_input_state":299,"lowest_start_state":310,"lowest_end_state":349,"active":false}}}');
INSERT INTO tmp_state_matrix_seed (config_key, config_value) VALUES ('reqRulDelStateMatrixDefault', '{"config_value":{"request":{"matrix":{"0":[0,49,620],"49":[49,620],"620":[620]},"derived_states":{"0":0,"49":49,"620":620},"lowest_input_state":0,"lowest_start_state":49,"lowest_end_state":49,"active":true},"approval":{"matrix":{"49":[60],"60":[60,99,610],"99":[99],"610":[610]},"derived_states":{"49":49,"60":60,"99":99,"610":610},"lowest_input_state":49,"lowest_start_state":60,"lowest_end_state":99,"active":true},"planning":{"matrix":{"99":[110],"110":[110,120,130,149],"120":[120,110,130,149],"130":[130,110,120,149,610],"149":[149],"610":[610]},"derived_states":{"99":99,"110":110,"120":110,"130":110,"149":149,"610":610},"lowest_input_state":99,"lowest_start_state":110,"lowest_end_state":149,"active":false},"verification":{"matrix":{"149":[160],"160":[160,199,610],"199":[199],"610":[610]},"derived_states":{"149":149,"160":160,"199":199,"610":610},"lowest_input_state":149,"lowest_start_state":160,"lowest_end_state":199,"active":false},"implementation":{"matrix":{"99":[210],"210":[210,220,249],"220":[220,210,249,610],"249":[249],"610":[610]},"derived_states":{"99":99,"210":210,"220":210,"249":249,"610":610},"lowest_input_state":99,"lowest_start_state":210,"lowest_end_state":249,"active":true},"review":{"matrix":{"249":[260],"260":[260,270,299],"270":[210,270,260,299,610],"299":[299],"610":[610]},"derived_states":{"249":249,"260":260,"270":260,"299":299,"610":610},"lowest_input_state":249,"lowest_start_state":260,"lowest_end_state":299,"active":false},"recertification":{"matrix":{"299":[310],"310":[310,349,400],"349":[349],"400":[400]},"derived_states":{"299":299,"310":310,"349":349,"400":400},"lowest_input_state":299,"lowest_start_state":310,"lowest_end_state":349,"active":false}}}');
INSERT INTO tmp_state_matrix_seed (config_key, config_value) VALUES ('reqRulModStateMatrixDefault', '{"config_value":{"request":{"matrix":{"0":[0,49,620],"49":[49,620],"620":[620]},"derived_states":{"0":0,"49":49,"620":620},"lowest_input_state":0,"lowest_start_state":49,"lowest_end_state":49,"active":true},"approval":{"matrix":{"49":[60],"60":[60,99,610],"99":[99],"610":[610]},"derived_states":{"49":49,"60":60,"99":99,"610":610},"lowest_input_state":49,"lowest_start_state":60,"lowest_end_state":99,"active":true},"planning":{"matrix":{"99":[110],"110":[110,120,130,149],"120":[120,110,130,149],"130":[130,110,120,149,610],"149":[149],"610":[610]},"derived_states":{"99":99,"110":110,"120":110,"130":110,"149":149,"610":610},"lowest_input_state":99,"lowest_start_state":110,"lowest_end_state":149,"active":false},"verification":{"matrix":{"149":[160],"160":[160,199,610],"199":[199],"610":[610]},"derived_states":{"149":149,"160":160,"199":199,"610":610},"lowest_input_state":149,"lowest_start_state":160,"lowest_end_state":199,"active":false},"implementation":{"matrix":{"99":[210],"210":[210,220,249],"220":[220,210,249,610],"249":[249],"610":[610]},"derived_states":{"99":99,"210":210,"220":210,"249":249,"610":610},"lowest_input_state":99,"lowest_start_state":210,"lowest_end_state":249,"active":true},"review":{"matrix":{"249":[260],"260":[260,270,299],"270":[210,270,260,299,610],"299":[299],"610":[610]},"derived_states":{"249":249,"260":260,"270":260,"299":299,"610":610},"lowest_input_state":249,"lowest_start_state":260,"lowest_end_state":299,"active":false},"recertification":{"matrix":{"299":[310],"310":[310,349,400],"349":[349],"400":[400]},"derived_states":{"299":299,"310":310,"349":349,"400":400},"lowest_input_state":299,"lowest_start_state":310,"lowest_end_state":349,"active":false}}}');
INSERT INTO tmp_state_matrix_seed (config_key, config_value) VALUES ('reqGrpCreStateMatrixDefault', '{"config_value":{"request":{"matrix":{"0":[0,49,620],"49":[49,620],"620":[620]},"derived_states":{"0":0,"49":49,"620":620},"lowest_input_state":0,"lowest_start_state":49,"lowest_end_state":49,"active":true},"approval":{"matrix":{"49":[60],"60":[60,99,610],"99":[99],"610":[610]},"derived_states":{"49":49,"60":60,"99":99,"610":610},"lowest_input_state":49,"lowest_start_state":60,"lowest_end_state":99,"active":true},"planning":{"matrix":{"99":[110],"110":[110,120,130,149],"120":[120,110,130,149],"130":[130,110,120,149,610],"149":[149],"610":[610]},"derived_states":{"99":99,"110":110,"120":110,"130":110,"149":149,"610":610},"lowest_input_state":99,"lowest_start_state":110,"lowest_end_state":149,"active":false},"verification":{"matrix":{"149":[160],"160":[160,199,610],"199":[199],"610":[610]},"derived_states":{"149":149,"160":160,"199":199,"610":610},"lowest_input_state":149,"lowest_start_state":160,"lowest_end_state":199,"active":false},"implementation":{"matrix":{"99":[210],"210":[210,220,249],"220":[220,210,249,610],"249":[249],"610":[610]},"derived_states":{"99":99,"210":210,"220":210,"249":249,"610":610},"lowest_input_state":99,"lowest_start_state":210,"lowest_end_state":249,"active":true},"review":{"matrix":{"249":[260],"260":[260,270,299],"270":[210,270,260,299,610],"299":[299],"610":[610]},"derived_states":{"249":249,"260":260,"270":260,"299":299,"610":610},"lowest_input_state":249,"lowest_start_state":260,"lowest_end_state":299,"active":false},"recertification":{"matrix":{"299":[310],"310":[310,349,400],"349":[349],"400":[400]},"derived_states":{"299":299,"310":310,"349":349,"400":400},"lowest_input_state":299,"lowest_start_state":310,"lowest_end_state":349,"active":false}}}');
INSERT INTO tmp_state_matrix_seed (config_key, config_value) VALUES ('reqGrpModStateMatrixDefault', '{"config_value":{"request":{"matrix":{"0":[0,49,620],"49":[49,620],"620":[620]},"derived_states":{"0":0,"49":49,"620":620},"lowest_input_state":0,"lowest_start_state":49,"lowest_end_state":49,"active":true},"approval":{"matrix":{"49":[60],"60":[60,99,610],"99":[99],"610":[610]},"derived_states":{"49":49,"60":60,"99":99,"610":610},"lowest_input_state":49,"lowest_start_state":60,"lowest_end_state":99,"active":true},"planning":{"matrix":{"99":[110],"110":[110,120,130,149],"120":[120,110,130,149],"130":[130,110,120,149,610],"149":[149],"610":[610]},"derived_states":{"99":99,"110":110,"120":110,"130":110,"149":149,"610":610},"lowest_input_state":99,"lowest_start_state":110,"lowest_end_state":149,"active":false},"verification":{"matrix":{"149":[160],"160":[160,199,610],"199":[199],"610":[610]},"derived_states":{"149":149,"160":160,"199":199,"610":610},"lowest_input_state":149,"lowest_start_state":160,"lowest_end_state":199,"active":false},"implementation":{"matrix":{"99":[210],"210":[210,220,249],"220":[220,210,249,610],"249":[249],"610":[610]},"derived_states":{"99":99,"210":210,"220":210,"249":249,"610":610},"lowest_input_state":99,"lowest_start_state":210,"lowest_end_state":249,"active":true},"review":{"matrix":{"249":[260],"260":[260,270,299],"270":[210,270,260,299,610],"299":[299],"610":[610]},"derived_states":{"249":249,"260":260,"270":260,"299":299,"610":610},"lowest_input_state":249,"lowest_start_state":260,"lowest_end_state":299,"active":false},"recertification":{"matrix":{"299":[310],"310":[310,349,400],"349":[349],"400":[400]},"derived_states":{"299":299,"310":310,"349":349,"400":400},"lowest_input_state":299,"lowest_start_state":310,"lowest_end_state":349,"active":false}}}');
INSERT INTO tmp_state_matrix_seed (config_key, config_value) VALUES ('reqGrpDelStateMatrixDefault', '{"config_value":{"request":{"matrix":{"0":[0,49,620],"49":[49,620],"620":[620]},"derived_states":{"0":0,"49":49,"620":620},"lowest_input_state":0,"lowest_start_state":49,"lowest_end_state":49,"active":true},"approval":{"matrix":{"49":[60],"60":[60,99,610],"99":[99],"610":[610]},"derived_states":{"49":49,"60":60,"99":99,"610":610},"lowest_input_state":49,"lowest_start_state":60,"lowest_end_state":99,"active":true},"planning":{"matrix":{"99":[110],"110":[110,120,130,149],"120":[120,110,130,149],"130":[130,110,120,149,610],"149":[149],"610":[610]},"derived_states":{"99":99,"110":110,"120":110,"130":110,"149":149,"610":610},"lowest_input_state":99,"lowest_start_state":110,"lowest_end_state":149,"active":false},"verification":{"matrix":{"149":[160],"160":[160,199,610],"199":[199],"610":[610]},"derived_states":{"149":149,"160":160,"199":199,"610":610},"lowest_input_state":149,"lowest_start_state":160,"lowest_end_state":199,"active":false},"implementation":{"matrix":{"99":[210],"210":[210,220,249],"220":[220,210,249,610],"249":[249],"610":[610]},"derived_states":{"99":99,"210":210,"220":210,"249":249,"610":610},"lowest_input_state":99,"lowest_start_state":210,"lowest_end_state":249,"active":true},"review":{"matrix":{"249":[260],"260":[260,270,299],"270":[210,270,260,299,610],"299":[299],"610":[610]},"derived_states":{"249":249,"260":260,"270":260,"299":299,"610":610},"lowest_input_state":249,"lowest_start_state":260,"lowest_end_state":299,"active":false},"recertification":{"matrix":{"299":[310],"310":[310,349,400],"349":[349],"400":[400]},"derived_states":{"299":299,"310":310,"349":349,"400":400},"lowest_input_state":299,"lowest_start_state":310,"lowest_end_state":349,"active":false}}}');
INSERT INTO tmp_state_matrix_seed (config_key, config_value) VALUES ('reqNewIntStateMatrixDefault', '{"config_value":{"request":{"matrix":{"0":[0,49,620]},"derived_states":{"0":0},"lowest_input_state":0,"lowest_start_state":0,"lowest_end_state":49,"active":true},"approval":{"matrix":{"0":[0]},"derived_states":{"0":0},"lowest_input_state":0,"lowest_start_state":0,"lowest_end_state":0,"active":false},"planning":{"matrix":{"0":[0]},"derived_states":{"0":0},"lowest_input_state":0,"lowest_start_state":0,"lowest_end_state":0,"active":false},"verification":{"matrix":{"0":[0]},"derived_states":{"0":0},"lowest_input_state":0,"lowest_start_state":0,"lowest_end_state":0,"active":false},"implementation":{"matrix":{"205":[205,249],"49":[210],"210":[610,210,249]},"derived_states":{"205":205,"49":49,"210":210},"lowest_input_state":49,"lowest_start_state":205,"lowest_end_state":249,"active":true},"review":{"matrix":{"249":[249,205,299]},"derived_states":{"249":249},"lowest_input_state":249,"lowest_start_state":249,"lowest_end_state":299,"active":true},"recertification":{"matrix":{"0":[0]},"derived_states":{"0":0},"lowest_input_state":0,"lowest_start_state":0,"lowest_end_state":0,"active":false}}}');
insert into request.state (id,name) VALUES (0,'Draft');
insert into request.state (id,name) VALUES (49,'Requested');

insert into request.state (id,name) VALUES (50,'To Approve');
insert into request.state (id,name) VALUES (60,'In Approval');
insert into request.state (id,name) VALUES (99,'Approved');

insert into request.state (id,name) VALUES (100,'To Plan');
insert into request.state (id,name) VALUES (110,'In Planning');
insert into request.state (id,name) VALUES (120,'Wait For Approval');
insert into request.state (id,name) VALUES (130,'Compliance Violation');
insert into request.state (id,name) VALUES (149,'Planned');

insert into request.state (id,name) VALUES (150,'To Verify Plan');
insert into request.state (id,name) VALUES (160,'Plan In Verification');
insert into request.state (id,name) VALUES (199,'Plan Verified');

insert into request.state (id,name) VALUES (200,'To Implement');
insert into request.state (id,name) VALUES (205,'Rework');
insert into request.state (id,name) VALUES (210,'In Implementation');
insert into request.state (id,name) VALUES (220,'Implementation Trouble');
insert into request.state (id,name) VALUES (249,'Implemented');

insert into request.state (id,name) VALUES (250,'To Review');
insert into request.state (id,name) VALUES (260,'In Review');
insert into request.state (id,name) VALUES (270,'Further Work Requested');
insert into request.state (id,name) VALUES (299,'Verified');

insert into request.state (id,name) VALUES (300,'To Recertify');
insert into request.state (id,name) VALUES (310,'In Recertification');
insert into request.state (id,name) VALUES (349,'Recertified');
insert into request.state (id,name) VALUES (400,'Decertified');

insert into request.state (id,name) VALUES (500,'InProgress');

insert into request.state (id,name) VALUES (600,'Done');
insert into request.state (id,name) VALUES (610,'Rejected');
insert into request.state (id,name) VALUES (620,'Discarded');

DROP TABLE IF EXISTS pg_temp.tmp_state_matrix_key;
CREATE TEMP TABLE tmp_state_matrix_key (
    config_key Varchar,
    configuration_name Varchar,
    task_type Varchar
);

INSERT INTO tmp_state_matrix_key (config_key, configuration_name, task_type)
VALUES
    ('reqMasterStateMatrixDefault', 'installation-default', 'master'),
    ('reqGenStateMatrixDefault', 'installation-default', 'generic'),
    ('reqAccStateMatrixDefault', 'installation-default', 'access'),
    ('reqRulDelStateMatrixDefault', 'installation-default', 'rule_delete'),
    ('reqRulModStateMatrixDefault', 'installation-default', 'rule_modify'),
    ('reqGrpCreStateMatrixDefault', 'installation-default', 'group_create'),
    ('reqGrpModStateMatrixDefault', 'installation-default', 'group_modify'),
    ('reqGrpDelStateMatrixDefault', 'installation-default', 'group_delete'),
    ('reqNewIntStateMatrixDefault', 'installation-default', 'new_interface');

INSERT INTO request.workflow_configuration (name, description, is_active)
VALUES
    ('installation-default', 'Workflow configuration proposal delivered with the installation', TRUE)
ON CONFLICT (name) DO UPDATE SET
    description = EXCLUDED.description,
    is_active = EXCLUDED.is_active;

WITH phase_data AS (
    SELECT
        mk.configuration_name,
        mk.task_type,
        phase.key AS phase,
        phase.value AS phase_config,
        mk.configuration_name || '_' || mk.task_type || '_' || phase.key AS phase_name
    FROM tmp_state_matrix_key mk
    JOIN tmp_state_matrix_seed c ON c.config_key = mk.config_key
    CROSS JOIN LATERAL jsonb_each((c.config_value::jsonb)->'config_value') AS phase
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
ON CONFLICT (name) DO UPDATE SET
    phase = EXCLUDED.phase,
    active = EXCLUDED.active,
    lowest_input_state = EXCLUDED.lowest_input_state,
    lowest_start_state = EXCLUDED.lowest_start_state,
    lowest_end_state = EXCLUDED.lowest_end_state;

WITH phase_data AS (
    SELECT
        mk.configuration_name,
        mk.task_type,
        phase.key AS phase,
        mk.configuration_name || '_' || mk.task_type || '_' || phase.key AS phase_name
    FROM tmp_state_matrix_key mk
    JOIN tmp_state_matrix_seed c ON c.config_key = mk.config_key
    CROSS JOIN LATERAL jsonb_each((c.config_value::jsonb)->'config_value') AS phase
)
INSERT INTO request.workflow_configuration_phase (configuration_id, task_type, phase, phase_matrix_id)
SELECT configuration.id, phase_data.task_type, phase_data.phase, matrix_phase.id
FROM phase_data
JOIN request.workflow_configuration configuration ON configuration.name = phase_data.configuration_name
JOIN request.state_matrix_phase matrix_phase ON matrix_phase.name = phase_data.phase_name
ON CONFLICT (configuration_id, task_type, phase) DO UPDATE SET
    phase_matrix_id = EXCLUDED.phase_matrix_id;

WITH phase_data AS (
    SELECT
        mk.configuration_name,
        mk.task_type,
        phase.key AS phase,
        phase.value AS phase_config,
        matrix_phase.id AS phase_matrix_id
    FROM tmp_state_matrix_key mk
    JOIN tmp_state_matrix_seed c ON c.config_key = mk.config_key
    CROSS JOIN LATERAL jsonb_each((c.config_value::jsonb)->'config_value') AS phase
    JOIN request.state_matrix_phase matrix_phase ON matrix_phase.name = mk.configuration_name || '_' || mk.task_type || '_' || phase.key
),
transition_group_data AS (
    SELECT
        phase_matrix_id,
        phase,
        configuration_name || '_' || task_type || '_' || phase || '_transitions' AS group_name
    FROM phase_data
)
INSERT INTO request.state_matrix_transition_group (name, description, phase, visibility_group_id)
SELECT group_name, 'Installation default transitions for ' || group_name, phase, NULL
FROM transition_group_data
ON CONFLICT (name) DO UPDATE SET
    description = EXCLUDED.description,
    phase = EXCLUDED.phase,
    visibility_group_id = EXCLUDED.visibility_group_id;

WITH phase_data AS (
    SELECT
        mk.configuration_name,
        mk.task_type,
        phase.key AS phase,
        matrix_phase.id AS phase_matrix_id,
        mk.configuration_name || '_' || mk.task_type || '_' || phase.key || '_transitions' AS group_name
    FROM tmp_state_matrix_key mk
    JOIN tmp_state_matrix_seed c ON c.config_key = mk.config_key
    CROSS JOIN LATERAL jsonb_each((c.config_value::jsonb)->'config_value') AS phase
    JOIN request.state_matrix_phase matrix_phase ON matrix_phase.name = mk.configuration_name || '_' || mk.task_type || '_' || phase.key
)
INSERT INTO request.state_matrix_phase_transition_group (phase_matrix_id, transition_group_id, sort_order)
SELECT phase_data.phase_matrix_id, transition_group.id, 0
FROM phase_data
JOIN request.state_matrix_transition_group transition_group ON transition_group.name = phase_data.group_name
ON CONFLICT (phase_matrix_id, transition_group_id) DO UPDATE SET
    sort_order = EXCLUDED.sort_order;

WITH phase_data AS (
    SELECT
        mk.configuration_name,
        mk.task_type,
        phase.key AS phase,
        phase.value AS phase_config,
        transition_group.id AS transition_group_id
    FROM tmp_state_matrix_key mk
    JOIN tmp_state_matrix_seed c ON c.config_key = mk.config_key
    CROSS JOIN LATERAL jsonb_each((c.config_value::jsonb)->'config_value') AS phase
    JOIN request.state_matrix_transition_group transition_group ON transition_group.name = mk.configuration_name || '_' || mk.task_type || '_' || phase.key || '_transitions'
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
ON CONFLICT (transition_group_id, from_state_id, to_state_id) DO UPDATE SET
    sort_order = EXCLUDED.sort_order;

WITH phase_data AS (
    SELECT
        mk.configuration_name,
        mk.task_type,
        phase.key AS phase,
        phase.value AS phase_config,
        matrix_phase.id AS phase_matrix_id
    FROM tmp_state_matrix_key mk
    JOIN tmp_state_matrix_seed c ON c.config_key = mk.config_key
    CROSS JOIN LATERAL jsonb_each((c.config_value::jsonb)->'config_value') AS phase
    JOIN request.state_matrix_phase matrix_phase ON matrix_phase.name = mk.configuration_name || '_' || mk.task_type || '_' || phase.key
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
ON CONFLICT (phase_matrix_id, from_state_id) DO UPDATE SET
    derived_state_id = EXCLUDED.derived_state_id;

DROP TABLE IF EXISTS pg_temp.tmp_state_matrix_key;
DROP TABLE IF EXISTS pg_temp.tmp_state_matrix_seed;
