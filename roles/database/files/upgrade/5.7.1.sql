-- create schema
-- to re-init request module database changes, manually issue the following commands before upgrading to 5.7.1:
-- drop schema implementation CASCADE;
-- drop schema request CASCADE;
-- note: this will delete all ticket data

create schema if not exists request;
create schema if not exists implementation;

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'rule_field_enum') THEN
    CREATE TYPE rule_field_enum AS ENUM ('source', 'destination', 'service');
    END IF;
END
$$;

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'action_enum') THEN
    CREATE TYPE action_enum AS ENUM ('create', 'delete', 'modify');
    END IF;
END
$$;


-- create tables
create table if not exists request.task 
(
    id BIGSERIAL PRIMARY KEY,
    title VARCHAR,
    ticket_id bigint,
    task_number int,
    state_id int NOT NULL,
    task_type VARCHAR NOT NULL,
    request_action action_enum NOT NULL,
    rule_action int,
    rule_tracking int,
    start Timestamp,
    stop Timestamp,
    svc_grp_id int,
    nw_obj_grp_id int,
    user_grp_id int,
    free_text text,
    reason text,
    last_recert_date Timestamp,
	current_handler int,
    recent_handler int,
    assigned_group varchar,
	target_begin_date Timestamp,
	target_end_date Timestamp,
    device_id int
);

create table if not exists request.element 
(
    id BIGSERIAL PRIMARY KEY,
    request_action action_enum NOT NULL default 'create',
    task_id bigint,
    ip cidr,
    port int,
    ip_proto_id int,
    network_object_id bigint,
    service_id bigint,
    field rule_field_enum NOT NULL,
    user_id bigint,
    original_nat_id int
);

create table if not exists request.approval 
(
    id BIGSERIAL PRIMARY KEY,
    task_id bigint,
    date_opened Timestamp NOT NULL default CURRENT_TIMESTAMP,
    approver_group Varchar,
    approval_date Timestamp,
    approver Varchar,
	current_handler int,
	recent_handler int,
	assigned_group varchar,
    tenant_id int,
    initial_approval boolean not null default true,
	approval_deadline Timestamp,
	state_id int NOT NULL
);

create table if not exists request.ticket 
(
    id BIGSERIAL PRIMARY KEY,
    title VARCHAR NOT NULL,
    date_created Timestamp NOT NULL default CURRENT_TIMESTAMP,
    date_completed Timestamp,
    state_id int NOT NULL,
    requester_id int,
    requester_dn Varchar,
    requester_group Varchar,
	current_handler int,
	recent_handler int,
	assigned_group varchar,
    tenant_id int,
    reason text,
    external_ticket_id varchar,
	external_ticket_source int,
    ticket_deadline Timestamp,
    ticket_priority int
);

create table if not exists request.comment 
(
    id BIGSERIAL PRIMARY KEY,
    ref_id bigint,
	scope varchar,
	creation_date Timestamp,
	creator int,
	comment_text varchar
);

create table if not exists request.ticket_comment
(
    ticket_id bigint,
    comment_id bigint
);

create table if not exists request.reqtask_comment
(
    task_id bigint,
    comment_id bigint
);

create table if not exists request.approval_comment
(
    approval_id bigint,
    comment_id bigint
);

create table if not exists request.impltask_comment
(
    task_id bigint,
    comment_id bigint
);

create table if not exists request.state
(
    id Integer NOT NULL UNIQUE PRIMARY KEY,
    name Varchar NOT NULL
);

create table if not exists request.action
(
    id SERIAL PRIMARY KEY,
    name Varchar NOT NULL,
	action_type Varchar NOT NULL,
	scope Varchar,
    task_type Varchar,
	phase Varchar,
	event Varchar,
	external_parameters Varchar
);

create table if not exists request.state_action
(
    state_id int,
    action_id int
);

create table if not exists owner
(
    id SERIAL PRIMARY KEY,
    name Varchar NOT NULL,
    dn Varchar NOT NULL,
    group_dn Varchar NOT NULL,
    is_default boolean default false,
    tenant_id int,
    recert_interval interval,
    next_recert_date Timestamp,
    app_id_external varchar not null
);

create unique index if not exists only_one_default_owner on owner(is_default) 
where is_default = true;

create table if not exists owner_network
(
    id SERIAL PRIMARY KEY,
    owner_id int,
    ip cidr NOT NULL,
    port int,
    ip_proto_id int
);

create table if not exists request_owner
(
    request_task_id bigint,
    owner_id int
);

create table if not exists rule_owner
(
    owner_id int,
    rule_metadata_id bigint
);

create table if not exists implementation.element
(
    id BIGSERIAL PRIMARY KEY,
    implementation_action action_enum NOT NULL default 'create',
    implementation_task_id bigint,
    ip cidr,
    port int,
    ip_proto_id int,
    network_object_id bigint,
    service_id bigint,
    field rule_field_enum NOT NULL,
    user_id bigint,
    original_nat_id int
);

create table if not exists implementation.task
(
    id BIGSERIAL PRIMARY KEY,
    request_task_id bigint,
    task_number int,
    state_id int NOT NULL,
    device_id int,
    task_type VARCHAR NOT NULL,
    implementation_action action_enum NOT NULL,
    rule_action int,
    rule_tracking int,
    start timestamp,
    stop timestamp,
    svc_grp_id int,
    nw_obj_grp_id int,
    user_grp_id int,
    free_text text,
	current_handler int,
	recent_handler int,
	assigned_group varchar,
	target_begin_date Timestamp,
	target_end_date Timestamp
);

--- FOREIGN KEYS ---

--- Drop ---

--- request.task ---
ALTER TABLE request.task DROP CONSTRAINT IF EXISTS request_task_request_ticket_foreign_key;
ALTER TABLE request.task DROP CONSTRAINT IF EXISTS request_task_request_state_foreign_key;
ALTER TABLE request.task DROP CONSTRAINT IF EXISTS request_task_stm_action_foreign_key;
ALTER TABLE request.task DROP CONSTRAINT IF EXISTS request_task_stm_track_foreign_key;
ALTER TABLE request.task DROP CONSTRAINT IF EXISTS request_task_service_foreign_key;
ALTER TABLE request.task DROP CONSTRAINT IF EXISTS request_task_object_foreign_key;
ALTER TABLE request.task DROP CONSTRAINT IF EXISTS request_task_usergrp_foreign_key;
ALTER TABLE request.task DROP CONSTRAINT IF EXISTS request_task_current_handler_foreign_key;
ALTER TABLE request.task DROP CONSTRAINT IF EXISTS request_task_recent_handler_foreign_key;
ALTER TABLE request.task DROP CONSTRAINT IF EXISTS request_task_device_foreign_key;
ALTER TABLE request.task DROP CONSTRAINT IF EXISTS request_task_comment_foreign_key;
--- request.element ---
ALTER TABLE request.element DROP CONSTRAINT IF EXISTS request_element_request_task_foreign_key;
ALTER TABLE request.element DROP CONSTRAINT IF EXISTS request_element_proto_foreign_key;
ALTER TABLE request.element DROP CONSTRAINT IF EXISTS request_element_service_foreign_key;
ALTER TABLE request.element DROP CONSTRAINT IF EXISTS request_element_object_foreign_key;
ALTER TABLE request.element DROP CONSTRAINT IF EXISTS request_element_request_element_foreign_key;
ALTER TABLE request.element DROP CONSTRAINT IF EXISTS request_element_usr_foreign_key;
--- request.approval ---
ALTER TABLE request.approval DROP CONSTRAINT IF EXISTS request_approval_request_task_foreign_key;
ALTER TABLE request.approval DROP CONSTRAINT IF EXISTS request_approval_tenant_foreign_key;
ALTER TABLE request.approval DROP CONSTRAINT IF EXISTS request_approval_request_state_foreign_key;
ALTER TABLE request.approval DROP CONSTRAINT IF EXISTS request_approval_current_handler_foreign_key;
ALTER TABLE request.approval DROP CONSTRAINT IF EXISTS request_approval_recent_handler_foreign_key;
ALTER TABLE request.approval DROP CONSTRAINT IF EXISTS request_approval_comment_foreign_key;
--- request.ticket ---
ALTER TABLE request.ticket DROP CONSTRAINT IF EXISTS request_ticket_request_state_foreign_key;
ALTER TABLE request.ticket DROP CONSTRAINT IF EXISTS request_ticket_tenant_foreign_key;
ALTER TABLE request.ticket DROP CONSTRAINT IF EXISTS request_ticket_uiuser_foreign_key;
ALTER TABLE request.ticket DROP CONSTRAINT IF EXISTS request_ticket_current_handler_foreign_key;
ALTER TABLE request.ticket DROP CONSTRAINT IF EXISTS request_ticket_recent_handler_foreign_key;
ALTER TABLE request.ticket DROP CONSTRAINT IF EXISTS request_ticket_comment_foreign_key;
--- owner ---
ALTER TABLE owner DROP CONSTRAINT IF EXISTS owner_tenant_foreign_key;
--- comment ---
ALTER TABLE request.comment DROP CONSTRAINT IF EXISTS comment_uiuser_foreign_key;
--- owner_network ---
ALTER TABLE owner_network DROP CONSTRAINT IF EXISTS owner_network_proto_foreign_key;
ALTER TABLE owner_network DROP CONSTRAINT IF EXISTS owner_network_owner_foreign_key;
--- rule_owner ---
ALTER TABLE rule_owner DROP CONSTRAINT IF EXISTS rule_owner_rule_metadata_foreign_key;
ALTER TABLE rule_owner DROP CONSTRAINT IF EXISTS rule_owner_owner_foreign_key;
--- request_owner ---
ALTER TABLE request_owner DROP CONSTRAINT IF EXISTS request_owner_request_task_foreign_key;
ALTER TABLE request_owner DROP CONSTRAINT IF EXISTS request_owner_owner_foreign_key;
--- ticket_comment ---
ALTER TABLE ticket_comment DROP CONSTRAINT IF EXISTS ticket_comment_task_foreign_key;
ALTER TABLE ticket_comment DROP CONSTRAINT IF EXISTS ticket_comment_comment_foreign_key;
--- reqtask_comment ---
ALTER TABLE reqtask_comment DROP CONSTRAINT IF EXISTS reqtask_comment_task_foreign_key;
ALTER TABLE reqtask_comment DROP CONSTRAINT IF EXISTS reqtask_comment_comment_foreign_key;
--- approval_comment ---
ALTER TABLE approval_comment DROP CONSTRAINT IF EXISTS approval_comment_task_foreign_key;
ALTER TABLE approval_comment DROP CONSTRAINT IF EXISTS approval_comment_comment_foreign_key;
--- impltask_comment ---
ALTER TABLE impltask_comment DROP CONSTRAINT IF EXISTS impltask_comment_task_foreign_key;
ALTER TABLE impltask_comment DROP CONSTRAINT IF EXISTS impltask_comment_comment_foreign_key;
--- state_action ---
ALTER TABLE request.state_action DROP CONSTRAINT IF EXISTS state_action_state_foreign_key;
ALTER TABLE request.state_action DROP CONSTRAINT IF EXISTS state_action_action_foreign_key;
--- implemantation.element ---
ALTER TABLE implementation.element DROP CONSTRAINT IF EXISTS implementation_element_implementation_element_foreign_key;
ALTER TABLE implementation.element DROP CONSTRAINT IF EXISTS implementation_element_service_foreign_key;
ALTER TABLE implementation.element DROP CONSTRAINT IF EXISTS implementation_element_object_foreign_key;
ALTER TABLE implementation.element DROP CONSTRAINT IF EXISTS implementation_element_proto_foreign_key;
ALTER TABLE implementation.element DROP CONSTRAINT IF EXISTS implementation_element_implementation_task_foreign_key;
ALTER TABLE implementation.element DROP CONSTRAINT IF EXISTS implementation_element_usr_foreign_key;
--- implementation.task
ALTER TABLE implementation.task DROP CONSTRAINT IF EXISTS implementation_task_request_task_foreign_key;
ALTER TABLE implementation.task DROP CONSTRAINT IF EXISTS implementation_task_request_state_foreign_key;
ALTER TABLE implementation.task DROP CONSTRAINT IF EXISTS implementation_task_device_foreign_key;
ALTER TABLE implementation.task DROP CONSTRAINT IF EXISTS implementation_task_stm_action_foreign_key;
ALTER TABLE implementation.task DROP CONSTRAINT IF EXISTS implementation_task_stm_tracking_foreign_key;
ALTER TABLE implementation.task DROP CONSTRAINT IF EXISTS implementation_task_service_foreign_key;
ALTER TABLE implementation.task DROP CONSTRAINT IF EXISTS implementation_task_object_foreign_key;
ALTER TABLE implementation.task DROP CONSTRAINT IF EXISTS implementation_task_usergrp_foreign_key;
ALTER TABLE implementation.task DROP CONSTRAINT IF EXISTS implementation_task_current_handler_foreign_key;
ALTER TABLE implementation.task DROP CONSTRAINT IF EXISTS implementation_task_recent_handler_foreign_key;
ALTER TABLE implementation.task DROP CONSTRAINT IF EXISTS implementation_task_comment_foreign_key;

--- ADD ---

--- request.task ---
ALTER TABLE request.task ADD CONSTRAINT request_task_request_ticket_foreign_key FOREIGN KEY (ticket_id) REFERENCES request.ticket(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.task ADD CONSTRAINT request_task_request_state_foreign_key FOREIGN KEY (state_id) REFERENCES request.state(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.task ADD CONSTRAINT request_task_stm_action_foreign_key FOREIGN KEY (rule_action) REFERENCES stm_action(action_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.task ADD CONSTRAINT request_task_stm_track_foreign_key FOREIGN KEY (rule_tracking) REFERENCES stm_track(track_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.task ADD CONSTRAINT request_task_service_foreign_key FOREIGN KEY (svc_grp_id) REFERENCES service(svc_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.task ADD CONSTRAINT request_task_object_foreign_key FOREIGN KEY (nw_obj_grp_id) REFERENCES object(obj_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.task ADD CONSTRAINT request_task_usergrp_foreign_key FOREIGN KEY (user_grp_id) REFERENCES usr(user_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.task ADD CONSTRAINT request_task_current_handler_foreign_key FOREIGN KEY (current_handler) REFERENCES uiuser(uiuser_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.task ADD CONSTRAINT request_task_recent_handler_foreign_key FOREIGN KEY (recent_handler) REFERENCES uiuser(uiuser_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.task ADD CONSTRAINT request_task_device_foreign_key FOREIGN KEY (device_id) REFERENCES device(dev_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.task ADD CONSTRAINT request_task_comment_foreign_key FOREIGN KEY (ref_comment) REFERENCES request.comment(id) ON UPDATE RESTRICT ON DELETE CASCADE;
--- request.element ---
ALTER TABLE request.element ADD CONSTRAINT request_element_request_task_foreign_key FOREIGN KEY (task_id) REFERENCES request.task(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.element ADD CONSTRAINT request_element_proto_foreign_key FOREIGN KEY (ip_proto_id) REFERENCES stm_ip_proto(ip_proto_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.element ADD CONSTRAINT request_element_service_foreign_key FOREIGN KEY (service_id) REFERENCES service(svc_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.element ADD CONSTRAINT request_element_object_foreign_key FOREIGN KEY (network_object_id) REFERENCES object(obj_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.element ADD CONSTRAINT request_element_request_element_foreign_key FOREIGN KEY (original_nat_id) REFERENCES request.element(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.element ADD CONSTRAINT request_element_usr_foreign_key FOREIGN KEY (user_id) REFERENCES usr(user_id) ON UPDATE RESTRICT ON DELETE CASCADE;
--- request.approval ---
ALTER TABLE request.approval ADD CONSTRAINT request_approval_request_task_foreign_key FOREIGN KEY (task_id) REFERENCES request.task(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.approval ADD CONSTRAINT request_approval_tenant_foreign_key FOREIGN KEY (tenant_id) REFERENCES tenant(tenant_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.approval ADD CONSTRAINT request_approval_request_state_foreign_key FOREIGN KEY (state_id) REFERENCES request.state(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.approval ADD CONSTRAINT request_approval_current_handler_foreign_key FOREIGN KEY (current_handler) REFERENCES uiuser(uiuser_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.approval ADD CONSTRAINT request_approval_recent_handler_foreign_key FOREIGN KEY (recent_handler) REFERENCES uiuser(uiuser_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.approval ADD CONSTRAINT request_approval_comment_foreign_key FOREIGN KEY (ref_comment) REFERENCES request.comment(id) ON UPDATE RESTRICT ON DELETE CASCADE;
--- request.ticket ---
ALTER TABLE request.ticket ADD CONSTRAINT request_ticket_request_state_foreign_key FOREIGN KEY (state_id) REFERENCES request.state(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.ticket ADD CONSTRAINT request_ticket_tenant_foreign_key FOREIGN KEY (tenant_id) REFERENCES tenant(tenant_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.ticket ADD CONSTRAINT request_ticket_uiuser_foreign_key FOREIGN KEY (requester_id) REFERENCES uiuser(uiuser_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.ticket ADD CONSTRAINT request_ticket_current_handler_foreign_key FOREIGN KEY (current_handler) REFERENCES uiuser(uiuser_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.ticket ADD CONSTRAINT request_ticket_recent_handler_foreign_key FOREIGN KEY (recent_handler) REFERENCES uiuser(uiuser_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.ticket ADD CONSTRAINT request_ticket_comment_foreign_key FOREIGN KEY (ref_comment) REFERENCES request.comment(id) ON UPDATE RESTRICT ON DELETE CASCADE;
--- owner ---
ALTER TABLE owner ADD CONSTRAINT owner_tenant_foreign_key FOREIGN KEY (tenant_id) REFERENCES tenant(tenant_id) ON UPDATE RESTRICT ON DELETE CASCADE;
--- comment ---
ALTER TABLE request.comment ADD CONSTRAINT comment_uiuser_foreign_key FOREIGN KEY (creator_id) REFERENCES uiuser(uiuser_id) ON UPDATE RESTRICT ON DELETE CASCADE;
--- owner_network ---
ALTER TABLE owner_network ADD CONSTRAINT owner_network_proto_foreign_key FOREIGN KEY (ip_proto_id) REFERENCES stm_ip_proto(ip_proto_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE owner_network ADD CONSTRAINT owner_network_owner_foreign_key FOREIGN KEY (owner_id) REFERENCES owner(id) ON UPDATE RESTRICT ON DELETE CASCADE;
--- rule_owner ---
ALTER TABLE rule_owner ADD CONSTRAINT rule_owner_rule_metadata_foreign_key FOREIGN KEY (rule_metadata_id) REFERENCES rule_metadata(rule_metadata_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE rule_owner ADD CONSTRAINT rule_owner_owner_foreign_key FOREIGN KEY (owner_id) REFERENCES owner(id) ON UPDATE RESTRICT ON DELETE CASCADE;
--- request_owner ---
ALTER TABLE request_owner ADD CONSTRAINT request_owner_request_task_foreign_key FOREIGN KEY (request_task_id) REFERENCES request.task(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request_owner ADD CONSTRAINT request_owner_owner_foreign_key FOREIGN KEY (owner_id) REFERENCES owner(id) ON UPDATE RESTRICT ON DELETE CASCADE;
--- ticket_comment ---
ALTER TABLE ticket_comment ADD CONSTRAINT ticket_comment_task_foreign_key FOREIGN KEY (ticket_id) REFERENCES request.ticket(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE ticket_comment ADD CONSTRAINT ticket_comment_comment_foreign_key FOREIGN KEY (comment_id) REFERENCES comment(id) ON UPDATE RESTRICT ON DELETE CASCADE;
--- reqtask_comment ---
ALTER TABLE reqtask_comment ADD CONSTRAINT reqtask_comment_task_foreign_key FOREIGN KEY (task_id) REFERENCES request.task(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE reqtask_comment ADD CONSTRAINT reqtask_comment_comment_foreign_key FOREIGN KEY (comment_id) REFERENCES comment(id) ON UPDATE RESTRICT ON DELETE CASCADE;
--- approval_comment ---
ALTER TABLE approval_comment ADD CONSTRAINT approval_comment_task_foreign_key FOREIGN KEY (approval_id) REFERENCES request.approval(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE approval_comment ADD CONSTRAINT approval_comment_comment_foreign_key FOREIGN KEY (comment_id) REFERENCES comment(id) ON UPDATE RESTRICT ON DELETE CASCADE;
--- impltask_comment ---
ALTER TABLE impltask_comment ADD CONSTRAINT impltask_comment_task_foreign_key FOREIGN KEY (task_id) REFERENCES implementation.task(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE impltask_comment ADD CONSTRAINT impltask_comment_comment_foreign_key FOREIGN KEY (comment_id) REFERENCES comment(id) ON UPDATE RESTRICT ON DELETE CASCADE;
--- state_action ---
ALTER TABLE request.state_action ADD CONSTRAINT state_action_state_foreign_key FOREIGN KEY (state_id) REFERENCES request.state(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.state_action ADD CONSTRAINT state_action_action_foreign_key FOREIGN KEY (action_id) REFERENCES request.action(id) ON UPDATE RESTRICT ON DELETE CASCADE;
--- implemantation.element ---
ALTER TABLE implementation.element ADD CONSTRAINT implementation_element_implementation_element_foreign_key FOREIGN KEY (original_nat_id) REFERENCES implementation.element(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE implementation.element ADD CONSTRAINT implementation_element_service_foreign_key FOREIGN KEY (service_id) REFERENCES service(svc_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE implementation.element ADD CONSTRAINT implementation_element_object_foreign_key FOREIGN KEY (network_object_id) REFERENCES object(obj_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE implementation.element ADD CONSTRAINT implementation_element_proto_foreign_key FOREIGN KEY (ip_proto_id) REFERENCES stm_ip_proto(ip_proto_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE implementation.element ADD CONSTRAINT implementation_element_implementation_task_foreign_key FOREIGN KEY (implementation_task_id) REFERENCES implementation.task(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE implementation.element ADD CONSTRAINT implementation_element_usr_foreign_key FOREIGN KEY (user_id) REFERENCES usr(user_id) ON UPDATE RESTRICT ON DELETE CASCADE;
--- implementation.task
ALTER TABLE implementation.task ADD CONSTRAINT implementation_task_request_task_foreign_key FOREIGN KEY (request_task_id) REFERENCES request.task(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE implementation.task ADD CONSTRAINT implementation_task_request_state_foreign_key FOREIGN KEY (state_id) REFERENCES request.state(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE implementation.task ADD CONSTRAINT implementation_task_device_foreign_key FOREIGN KEY (device_id) REFERENCES device(dev_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE implementation.task ADD CONSTRAINT implementation_task_stm_action_foreign_key FOREIGN KEY (rule_action) REFERENCES stm_action(action_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE implementation.task ADD CONSTRAINT implementation_task_stm_tracking_foreign_key FOREIGN KEY (rule_tracking) REFERENCES stm_track(track_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE implementation.task ADD CONSTRAINT implementation_task_service_foreign_key FOREIGN KEY (svc_grp_id) REFERENCES service(svc_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE implementation.task ADD CONSTRAINT implementation_task_object_foreign_key FOREIGN KEY (nw_obj_grp_id) REFERENCES object(obj_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE implementation.task ADD CONSTRAINT implementation_task_usergrp_foreign_key FOREIGN KEY (user_grp_id) REFERENCES usr(user_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE implementation.task ADD CONSTRAINT implementation_task_current_handler_foreign_key FOREIGN KEY (current_handler) REFERENCES uiuser(uiuser_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE implementation.task ADD CONSTRAINT implementation_task_recent_handler_foreign_key FOREIGN KEY (recent_handler) REFERENCES uiuser(uiuser_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE implementation.task ADD CONSTRAINT implementation_task_comment_foreign_key FOREIGN KEY (ref_comment) REFERENCES request.comment(id) ON UPDATE RESTRICT ON DELETE CASCADE;

--- OTHER CONSTRAINTS ---

--- DELETE ---

--- owner_network ---
ALTER TABLE owner_network DROP CONSTRAINT IF EXISTS port_in_valid_range;
--- request.element ---
ALTER TABLE request.element DROP CONSTRAINT IF EXISTS port_in_valid_range;
--- implementation.element ---
ALTER TABLE implementation.element DROP CONSTRAINT IF EXISTS port_in_valid_range;

--- ADD ---

--- owner_network ---
ALTER TABLE owner_network ADD CONSTRAINT port_in_valid_range CHECK (port > 0 and port <= 65535);
--- request.element ---
ALTER TABLE request.element ADD CONSTRAINT port_in_valid_range CHECK (port > 0 and port <= 65535);
--- implementation.element ---
ALTER TABLE implementation.element ADD CONSTRAINT port_in_valid_range CHECK (port > 0 and port <= 65535);


-- setting indices on view_rule_change to improve performance
-- DROP index if exists idx_changelog_rule04;
-- Create index IF NOT EXISTS idx_changelog_rule04 on changelog_rule (change_action);

-- DROP index if exists idx_changelog_rule05;
-- Create index IF NOT EXISTS idx_changelog_rule05 on changelog_rule (new_rule_id);

-- DROP index if exists idx_changelog_rule06;
-- Create index IF NOT EXISTS idx_changelog_rule06 on changelog_rule (old_rule_id);

-- DROP index if exists idx_rule04;
-- Create index IF NOT EXISTS idx_rule04 on rule (access_rule);


-- alter table report add column if not exists report_schedule_id bigint;
-- ALTER TABLE report DROP CONSTRAINT IF EXISTS "report_report_schedule_report_schedule_id_fkey" CASCADE;
-- Alter table report add CONSTRAINT report_report_schedule_report_schedule_id_fkey foreign key (report_schedule_id) references report_schedule (report_schedule_id) on update restrict on delete cascade;

insert into config (config_key, config_value, config_user) VALUES ('importCheckCertificates', 'False', 0) ON CONFLICT DO NOTHING;
insert into config (config_key, config_value, config_user) VALUES ('importSuppressCertificateWarnings', 'True', 0) ON CONFLICT DO NOTHING;
insert into config (config_key, config_value, config_user) VALUES ('sessionTimeout', '240', 0) ON CONFLICT DO NOTHING;
insert into config (config_key, config_value, config_user) VALUES ('sessionTimeoutNoticePeriod', '60', 0) ON CONFLICT DO NOTHING;
-- insert into config (config_key, config_value, config_user) VALUES ('maxMessages', '3', 0) ON CONFLICT DO NOTHING;

insert into config (config_key, config_value, config_user) VALUES ('reqMasterStateMatrix', '{"config_value":{"request":{"matrix":{"0":[0,49,620],"49":[49,620],"620":[620]},"derived_states":{"0":0,"49":49,"620":620},"lowest_input_state":0,"lowest_start_state":49,"lowest_end_state":49,"active":true},"approval":{"matrix":{"49":[60],"60":[60,99,610],"99":[99],"610":[610]},"derived_states":{"49":49,"60":60,"99":99,"610":610},"lowest_input_state":49,"lowest_start_state":60,"lowest_end_state":99,"active":true},"planning":{"matrix":{"99":[110],"110":[110,120,130,149],"120":[120,110,130,149],"130":[130,110,120,149,610],"149":[149],"610":[610]},"derived_states":{"99":99,"110":110,"120":110,"130":110,"149":149,"610":610},"lowest_input_state":99,"lowest_start_state":110,"lowest_end_state":149,"active":false},"verification":{"matrix":{"149":[160],"160":[160,199,610],"199":[199],"610":[610]},"derived_states":{"149":149,"160":160,"199":199,"610":610},"lowest_input_state":149,"lowest_start_state":160,"lowest_end_state":199,"active":false},"implementation":{"matrix":{"99":[210],"210":[210,220,249],"220":[220,210,249,610],"249":[249],"610":[610]},"derived_states":{"99":99,"210":210,"220":210,"249":249,"610":610},"lowest_input_state":99,"lowest_start_state":210,"lowest_end_state":249,"active":true},"review":{"matrix":{"249":[260],"260":[260,270,299],"270":[210,270,260,299,610],"299":[299],"610":[610]},"derived_states":{"249":249,"260":260,"270":260,"299":299,"610":610},"lowest_input_state":249,"lowest_start_state":260,"lowest_end_state":299,"active":false},"recertification":{"matrix":{"299":[310],"310":[310,349,400],"349":[349],"400":[400]},"derived_states":{"299":299,"310":310,"349":349,"400":400},"lowest_input_state":299,"lowest_start_state":310,"lowest_end_state":349,"active":false}}}', 0) ON CONFLICT DO NOTHING;
insert into config (config_key, config_value, config_user) VALUES ('reqGenStateMatrix', '{"config_value":{"request":{"matrix":{"0":[0,49,620],"49":[49,620],"620":[620]},"derived_states":{"0":0,"49":49,"620":620},"lowest_input_state":0,"lowest_start_state":49,"lowest_end_state":49,"active":true},"approval":{"matrix":{"49":[60],"60":[60,99,610],"99":[99],"610":[610]},"derived_states":{"49":49,"60":60,"99":99,"610":610},"lowest_input_state":49,"lowest_start_state":60,"lowest_end_state":99,"active":true},"planning":{"matrix":{"99":[110],"110":[110,120,130,149],"120":[120,110,130,149],"130":[130,110,120,149,610],"149":[149],"610":[610]},"derived_states":{"99":99,"110":110,"120":110,"130":110,"149":149,"610":610},"lowest_input_state":99,"lowest_start_state":110,"lowest_end_state":149,"active":false},"verification":{"matrix":{"149":[160],"160":[160,199,610],"199":[199],"610":[610]},"derived_states":{"149":149,"160":160,"199":199,"610":610},"lowest_input_state":149,"lowest_start_state":160,"lowest_end_state":199,"active":false},"implementation":{"matrix":{"99":[210],"210":[210,220,249],"220":[220,210,249,610],"249":[249],"610":[610]},"derived_states":{"99":99,"210":210,"220":210,"249":249,"610":610},"lowest_input_state":99,"lowest_start_state":210,"lowest_end_state":249,"active":true},"review":{"matrix":{"249":[260],"260":[260,270,299],"270":[210,270,260,299,610],"299":[299],"610":[610]},"derived_states":{"249":249,"260":260,"270":260,"299":299,"610":610},"lowest_input_state":249,"lowest_start_state":260,"lowest_end_state":299,"active":false},"recertification":{"matrix":{"299":[310],"310":[310,349,400],"349":[349],"400":[400]},"derived_states":{"299":299,"310":310,"349":349,"400":400},"lowest_input_state":299,"lowest_start_state":310,"lowest_end_state":349,"active":false}}}', 0) ON CONFLICT DO NOTHING;
insert into config (config_key, config_value, config_user) VALUES ('reqAccStateMatrix', '{"config_value":{"request":{"matrix":{"0":[0,49,620],"49":[49,620],"620":[620]},"derived_states":{"0":0,"49":49,"620":620},"lowest_input_state":0,"lowest_start_state":49,"lowest_end_state":49,"active":true},"approval":{"matrix":{"49":[60],"60":[60,99,610],"99":[99],"610":[610]},"derived_states":{"49":49,"60":60,"99":99,"610":610},"lowest_input_state":49,"lowest_start_state":60,"lowest_end_state":99,"active":true},"planning":{"matrix":{"99":[110],"110":[110,120,130,149],"120":[120,110,130,149],"130":[130,110,120,149,610],"149":[149],"610":[610]},"derived_states":{"99":99,"110":110,"120":110,"130":110,"149":149,"610":610},"lowest_input_state":99,"lowest_start_state":110,"lowest_end_state":149,"active":false},"verification":{"matrix":{"149":[160],"160":[160,199,610],"199":[199],"610":[610]},"derived_states":{"149":149,"160":160,"199":199,"610":610},"lowest_input_state":149,"lowest_start_state":160,"lowest_end_state":199,"active":false},"implementation":{"matrix":{"99":[210],"210":[210,220,249],"220":[220,210,249,610],"249":[249],"610":[610]},"derived_states":{"99":99,"210":210,"220":210,"249":249,"610":610},"lowest_input_state":99,"lowest_start_state":210,"lowest_end_state":249,"active":true},"review":{"matrix":{"249":[260],"260":[260,270,299],"270":[210,270,260,299,610],"299":[299],"610":[610]},"derived_states":{"249":249,"260":260,"270":260,"299":299,"610":610},"lowest_input_state":249,"lowest_start_state":260,"lowest_end_state":299,"active":false},"recertification":{"matrix":{"299":[310],"310":[310,349,400],"349":[349],"400":[400]},"derived_states":{"299":299,"310":310,"349":349,"400":400},"lowest_input_state":299,"lowest_start_state":310,"lowest_end_state":349,"active":false}}}', 0) ON CONFLICT DO NOTHING;
insert into config (config_key, config_value, config_user) VALUES ('reqGrpStateMatrix', '{"config_value":{"request":{"matrix":{"0":[0,49,620],"49":[49,620],"620":[620]},"derived_states":{"0":0,"49":49,"620":620},"lowest_input_state":0,"lowest_start_state":49,"lowest_end_state":49,"active":true},"approval":{"matrix":{"49":[60],"60":[60,99,610],"99":[99],"610":[610]},"derived_states":{"49":49,"60":60,"99":99,"610":610},"lowest_input_state":49,"lowest_start_state":60,"lowest_end_state":99,"active":true},"planning":{"matrix":{"99":[110],"110":[110,120,130,149],"120":[120,110,130,149],"130":[130,110,120,149,610],"149":[149],"610":[610]},"derived_states":{"99":99,"110":110,"120":110,"130":110,"149":149,"610":610},"lowest_input_state":99,"lowest_start_state":110,"lowest_end_state":149,"active":false},"verification":{"matrix":{"149":[160],"160":[160,199,610],"199":[199],"610":[610]},"derived_states":{"149":149,"160":160,"199":199,"610":610},"lowest_input_state":149,"lowest_start_state":160,"lowest_end_state":199,"active":false},"implementation":{"matrix":{"99":[210],"210":[210,220,249],"220":[220,210,249,610],"249":[249],"610":[610]},"derived_states":{"99":99,"210":210,"220":210,"249":249,"610":610},"lowest_input_state":99,"lowest_start_state":210,"lowest_end_state":249,"active":true},"review":{"matrix":{"249":[260],"260":[260,270,299],"270":[210,270,260,299,610],"299":[299],"610":[610]},"derived_states":{"249":249,"260":260,"270":260,"299":299,"610":610},"lowest_input_state":249,"lowest_start_state":260,"lowest_end_state":299,"active":false},"recertification":{"matrix":{"299":[310],"310":[310,349,400],"349":[349],"400":[400]},"derived_states":{"299":299,"310":310,"349":349,"400":400},"lowest_input_state":299,"lowest_start_state":310,"lowest_end_state":349,"active":false}}}', 0) ON CONFLICT DO NOTHING;
insert into config (config_key, config_value, config_user) VALUES ('reqAvailableTaskTypes', '[0,1,2]', 0) ON CONFLICT DO NOTHING;
insert into config (config_key, config_value, config_user) VALUES ('reqPriorities', '[{"numeric_prio":1,"name":"Highest","ticket_deadline":1,"approval_deadline":1},{"numeric_prio":2,"name":"High","ticket_deadline":3,"approval_deadline":2},{"numeric_prio":3,"name":"Medium","ticket_deadline":7,"approval_deadline":3},{"numeric_prio":4,"name":"Low","ticket_deadline":14,"approval_deadline":7},{"numeric_prio":5,"name":"Lowest","ticket_deadline":30,"approval_deadline":14}]', 0) ON CONFLICT DO NOTHING;
insert into config (config_key, config_value, config_user) VALUES ('reqAutoCreateImplTasks', 'enterInReqTask', 0) ON CONFLICT DO NOTHING;
insert into config (config_key, config_value, config_user) VALUES ('reqAllowObjectSearch', 'False', 0) ON CONFLICT DO NOTHING;

insert into request.state (id,name) VALUES (0,'Draft') ON CONFLICT DO NOTHING;
insert into request.state (id,name) VALUES (49,'Requested') ON CONFLICT DO NOTHING;

insert into request.state (id,name) VALUES (50,'To Approve') ON CONFLICT DO NOTHING;
insert into request.state (id,name) VALUES (60,'In Approval') ON CONFLICT DO NOTHING;
insert into request.state (id,name) VALUES (99,'Approved') ON CONFLICT DO NOTHING;

insert into request.state (id,name) VALUES (100,'To Plan') ON CONFLICT DO NOTHING;
insert into request.state (id,name) VALUES (110,'In Planning') ON CONFLICT DO NOTHING;
insert into request.state (id,name) VALUES (120,'Wait For Approval') ON CONFLICT DO NOTHING;
insert into request.state (id,name) VALUES (130,'Compliance Violation') ON CONFLICT DO NOTHING;
insert into request.state (id,name) VALUES (149,'Planned') ON CONFLICT DO NOTHING;

insert into request.state (id,name) VALUES (150,'To Verify Plan') ON CONFLICT DO NOTHING;
insert into request.state (id,name) VALUES (160,'Plan In Verification') ON CONFLICT DO NOTHING;
insert into request.state (id,name) VALUES (199,'Plan Verified') ON CONFLICT DO NOTHING;

insert into request.state (id,name) VALUES (200,'To Implement') ON CONFLICT DO NOTHING;
insert into request.state (id,name) VALUES (210,'In Implementation') ON CONFLICT DO NOTHING;
insert into request.state (id,name) VALUES (220,'Implementation Trouble') ON CONFLICT DO NOTHING;
insert into request.state (id,name) VALUES (249,'Implemented') ON CONFLICT DO NOTHING;

insert into request.state (id,name) VALUES (250,'To Review') ON CONFLICT DO NOTHING;
insert into request.state (id,name) VALUES (260,'In Review') ON CONFLICT DO NOTHING;
insert into request.state (id,name) VALUES (270,'Further Work Requested') ON CONFLICT DO NOTHING;
insert into request.state (id,name) VALUES (299,'Verified') ON CONFLICT DO NOTHING;

insert into request.state (id,name) VALUES (300,'To Recertify') ON CONFLICT DO NOTHING;
insert into request.state (id,name) VALUES (310,'In Recertification') ON CONFLICT DO NOTHING;
insert into request.state (id,name) VALUES (349,'Recertified') ON CONFLICT DO NOTHING;
insert into request.state (id,name) VALUES (400,'Decertified') ON CONFLICT DO NOTHING;

insert into request.state (id,name) VALUES (500,'InProgress') ON CONFLICT DO NOTHING;

insert into request.state (id,name) VALUES (600,'Done') ON CONFLICT DO NOTHING;
insert into request.state (id,name) VALUES (610,'Rejected') ON CONFLICT DO NOTHING;
insert into request.state (id,name) VALUES (620,'Discarded') ON CONFLICT DO NOTHING;

-- add tenant_network demo data
DO $do$ BEGIN
    IF EXISTS (SELECT tenant_id FROM tenant WHERE tenant_name='tenant1_demo') THEN
        IF NOT EXISTS (SELECT * FROM tenant_network LEFT JOIN tenant USING (tenant_id) WHERE tenant_name='tenant1_demo' and tenant_net_ip='10.222.0.32/27') THEN
            insert into tenant_network (tenant_id, tenant_net_ip, tenant_net_comment) 
            VALUES ((SELECT tenant_id FROM tenant WHERE tenant_name='tenant1_demo'), '10.222.0.32/27', 'demo network for tenant 1') ON CONFLICT DO NOTHING;
        END IF;
    END IF;
    IF EXISTS (SELECT tenant_id FROM tenant WHERE tenant_name='tenant2_demo') THEN
        IF NOT EXISTS (SELECT * FROM tenant_network LEFT JOIN tenant USING (tenant_id) WHERE tenant_name='tenant2_demo' and tenant_net_ip='10.0.0.48/29') THEN
            insert into tenant_network (tenant_id, tenant_net_ip, tenant_net_comment) 
            VALUES ((SELECT tenant_id FROM tenant WHERE tenant_name='tenant2_demo'), '10.0.0.48/29', 'demo network for tenant 2') ON CONFLICT DO NOTHING;
        END IF;
    END IF;
END $do$; 
