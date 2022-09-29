-- create schema
-- to re-init request module database changes, manually issue the following commands before upgrading to 5.7.1:
-- drop schema request CASCADE;
-- note: this will delete all ticket data

create schema if not exists request;

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
create table if not exists request.reqtask 
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

create table if not exists request.reqelement 
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
	creator_id int,
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
	button_text Varchar,
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

create table if not exists reqtask_owner
(
    reqtask_id bigint,
    owner_id int
);

create table if not exists rule_owner
(
    owner_id int,
    rule_metadata_id bigint
);

create table if not exists request.implelement
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

create table if not exists request.impltask
(
    id BIGSERIAL PRIMARY KEY,
    title VARCHAR,
    reqtask_id bigint,
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

--- request.reqtask ---
ALTER TABLE request.reqtask DROP CONSTRAINT IF EXISTS request_reqtask_request_ticket_foreign_key;
ALTER TABLE request.reqtask DROP CONSTRAINT IF EXISTS request_reqtask_request_state_foreign_key;
ALTER TABLE request.reqtask DROP CONSTRAINT IF EXISTS request_reqtask_stm_action_foreign_key;
ALTER TABLE request.reqtask DROP CONSTRAINT IF EXISTS request_reqtask_stm_track_foreign_key;
ALTER TABLE request.reqtask DROP CONSTRAINT IF EXISTS request_reqtask_service_foreign_key;
ALTER TABLE request.reqtask DROP CONSTRAINT IF EXISTS request_reqtask_object_foreign_key;
ALTER TABLE request.reqtask DROP CONSTRAINT IF EXISTS request_reqtask_usergrp_foreign_key;
ALTER TABLE request.reqtask DROP CONSTRAINT IF EXISTS request_reqtask_current_handler_foreign_key;
ALTER TABLE request.reqtask DROP CONSTRAINT IF EXISTS request_reqtask_recent_handler_foreign_key;
ALTER TABLE request.reqtask DROP CONSTRAINT IF EXISTS request_reqtask_device_foreign_key;
--- request.reqelement ---
ALTER TABLE request.reqelement DROP CONSTRAINT IF EXISTS request_reqelement_request_reqtask_foreign_key;
ALTER TABLE request.reqelement DROP CONSTRAINT IF EXISTS request_reqelement_proto_foreign_key;
ALTER TABLE request.reqelement DROP CONSTRAINT IF EXISTS request_reqelement_service_foreign_key;
ALTER TABLE request.reqelement DROP CONSTRAINT IF EXISTS request_reqelement_object_foreign_key;
ALTER TABLE request.reqelement DROP CONSTRAINT IF EXISTS request_reqelement_request_reqelement_foreign_key;
ALTER TABLE request.reqelement DROP CONSTRAINT IF EXISTS request_reqelement_usr_foreign_key;
--- request.approval ---
ALTER TABLE request.approval DROP CONSTRAINT IF EXISTS request_approval_request_reqtask_foreign_key;
ALTER TABLE request.approval DROP CONSTRAINT IF EXISTS request_approval_tenant_foreign_key;
ALTER TABLE request.approval DROP CONSTRAINT IF EXISTS request_approval_request_state_foreign_key;
ALTER TABLE request.approval DROP CONSTRAINT IF EXISTS request_approval_current_handler_foreign_key;
ALTER TABLE request.approval DROP CONSTRAINT IF EXISTS request_approval_recent_handler_foreign_key;
--- request.ticket ---
ALTER TABLE request.ticket DROP CONSTRAINT IF EXISTS request_ticket_request_state_foreign_key;
ALTER TABLE request.ticket DROP CONSTRAINT IF EXISTS request_ticket_tenant_foreign_key;
ALTER TABLE request.ticket DROP CONSTRAINT IF EXISTS request_ticket_uiuser_foreign_key;
ALTER TABLE request.ticket DROP CONSTRAINT IF EXISTS request_ticket_current_handler_foreign_key;
ALTER TABLE request.ticket DROP CONSTRAINT IF EXISTS request_ticket_recent_handler_foreign_key;
--- owner ---
ALTER TABLE owner DROP CONSTRAINT IF EXISTS owner_tenant_foreign_key;
--- comment ---
ALTER TABLE request.comment DROP CONSTRAINT IF EXISTS request_comment_uiuser_foreign_key;
ALTER TABLE request.comment DROP CONSTRAINT IF EXISTS request_comment_request_comment_foreign_key;
--- owner_network ---
ALTER TABLE owner_network DROP CONSTRAINT IF EXISTS owner_network_proto_foreign_key;
ALTER TABLE owner_network DROP CONSTRAINT IF EXISTS owner_network_owner_foreign_key;
--- rule_owner ---
ALTER TABLE rule_owner DROP CONSTRAINT IF EXISTS rule_owner_rule_metadata_foreign_key;
ALTER TABLE rule_owner DROP CONSTRAINT IF EXISTS rule_owner_owner_foreign_key;
--- reqtask_owner ---
ALTER TABLE reqtask_owner DROP CONSTRAINT IF EXISTS reqtask_owner_request_reqtask_foreign_key;
ALTER TABLE reqtask_owner DROP CONSTRAINT IF EXISTS reqtask_owner_owner_foreign_key;
--- ticket_comment ---
ALTER TABLE request.ticket_comment DROP CONSTRAINT IF EXISTS request_ticket_comment_ticket_foreign_key;
ALTER TABLE request.ticket_comment DROP CONSTRAINT IF EXISTS request_ticket_comment_comment_foreign_key;
--- reqtask_comment ---
ALTER TABLE request.reqtask_comment DROP CONSTRAINT IF EXISTS request_reqtask_comment_reqtask_foreign_key;
ALTER TABLE request.reqtask_comment DROP CONSTRAINT IF EXISTS request_reqtask_comment_comment_foreign_key;
--- approval_comment ---
ALTER TABLE request.approval_comment DROP CONSTRAINT IF EXISTS request_approval_comment_approval_foreign_key;
ALTER TABLE request.approval_comment DROP CONSTRAINT IF EXISTS request_approval_comment_comment_foreign_key;
--- impltask_comment ---
ALTER TABLE request.impltask_comment DROP CONSTRAINT IF EXISTS request_impltask_comment_impltask_foreign_key;
ALTER TABLE request.impltask_comment DROP CONSTRAINT IF EXISTS request_impltask_comment_comment_foreign_key;
--- state_action ---
ALTER TABLE request.state_action DROP CONSTRAINT IF EXISTS request_state_action_state_foreign_key;
ALTER TABLE request.state_action DROP CONSTRAINT IF EXISTS request_state_action_action_foreign_key;
--- request.implelement ---
ALTER TABLE request.implelement DROP CONSTRAINT IF EXISTS request_implelement_request_implelement_foreign_key;
ALTER TABLE request.implelement DROP CONSTRAINT IF EXISTS request_implelement_service_foreign_key;
ALTER TABLE request.implelement DROP CONSTRAINT IF EXISTS request_implelement_object_foreign_key;
ALTER TABLE request.implelement DROP CONSTRAINT IF EXISTS request_implelement_proto_foreign_key;
ALTER TABLE request.implelement DROP CONSTRAINT IF EXISTS request_implelement_request_impltask_foreign_key;
ALTER TABLE request.implelement DROP CONSTRAINT IF EXISTS request_implelement_usr_foreign_key;
--- request.impltask
ALTER TABLE request.impltask DROP CONSTRAINT IF EXISTS request_impltask_request_reqtask_foreign_key;
ALTER TABLE request.impltask DROP CONSTRAINT IF EXISTS request_impltask_request_state_foreign_key;
ALTER TABLE request.impltask DROP CONSTRAINT IF EXISTS request_impltask_device_foreign_key;
ALTER TABLE request.impltask DROP CONSTRAINT IF EXISTS request_impltask_stm_action_foreign_key;
ALTER TABLE request.impltask DROP CONSTRAINT IF EXISTS request_impltask_stm_tracking_foreign_key;
ALTER TABLE request.impltask DROP CONSTRAINT IF EXISTS request_impltask_service_foreign_key;
ALTER TABLE request.impltask DROP CONSTRAINT IF EXISTS request_impltask_object_foreign_key;
ALTER TABLE request.impltask DROP CONSTRAINT IF EXISTS request_impltask_usergrp_foreign_key;
ALTER TABLE request.impltask DROP CONSTRAINT IF EXISTS request_impltask_current_handler_foreign_key;
ALTER TABLE request.impltask DROP CONSTRAINT IF EXISTS request_impltask_recent_handler_foreign_key;

--- ADD ---

--- request.reqtask ---
ALTER TABLE request.reqtask ADD CONSTRAINT request_reqtask_request_ticket_foreign_key FOREIGN KEY (ticket_id) REFERENCES request.ticket(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.reqtask ADD CONSTRAINT request_reqtask_request_state_foreign_key FOREIGN KEY (state_id) REFERENCES request.state(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.reqtask ADD CONSTRAINT request_reqtask_stm_action_foreign_key FOREIGN KEY (rule_action) REFERENCES stm_action(action_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.reqtask ADD CONSTRAINT request_reqtask_stm_track_foreign_key FOREIGN KEY (rule_tracking) REFERENCES stm_track(track_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.reqtask ADD CONSTRAINT request_reqtask_service_foreign_key FOREIGN KEY (svc_grp_id) REFERENCES service(svc_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.reqtask ADD CONSTRAINT request_reqtask_object_foreign_key FOREIGN KEY (nw_obj_grp_id) REFERENCES object(obj_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.reqtask ADD CONSTRAINT request_reqtask_usergrp_foreign_key FOREIGN KEY (user_grp_id) REFERENCES usr(user_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.reqtask ADD CONSTRAINT request_reqtask_current_handler_foreign_key FOREIGN KEY (current_handler) REFERENCES uiuser(uiuser_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.reqtask ADD CONSTRAINT request_reqtask_recent_handler_foreign_key FOREIGN KEY (recent_handler) REFERENCES uiuser(uiuser_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.reqtask ADD CONSTRAINT request_reqtask_device_foreign_key FOREIGN KEY (device_id) REFERENCES device(dev_id) ON UPDATE RESTRICT ON DELETE CASCADE;
--- request.reqelement ---
ALTER TABLE request.reqelement ADD CONSTRAINT request_reqelement_request_reqtask_foreign_key FOREIGN KEY (task_id) REFERENCES request.reqtask(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.reqelement ADD CONSTRAINT request_reqelement_proto_foreign_key FOREIGN KEY (ip_proto_id) REFERENCES stm_ip_proto(ip_proto_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.reqelement ADD CONSTRAINT request_reqelement_service_foreign_key FOREIGN KEY (service_id) REFERENCES service(svc_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.reqelement ADD CONSTRAINT request_reqelement_object_foreign_key FOREIGN KEY (network_object_id) REFERENCES object(obj_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.reqelement ADD CONSTRAINT request_reqelement_request_reqelement_foreign_key FOREIGN KEY (original_nat_id) REFERENCES request.reqelement(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.reqelement ADD CONSTRAINT request_reqelement_usr_foreign_key FOREIGN KEY (user_id) REFERENCES usr(user_id) ON UPDATE RESTRICT ON DELETE CASCADE;
--- request.approval ---
ALTER TABLE request.approval ADD CONSTRAINT request_approval_request_reqtask_foreign_key FOREIGN KEY (task_id) REFERENCES request.reqtask(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.approval ADD CONSTRAINT request_approval_tenant_foreign_key FOREIGN KEY (tenant_id) REFERENCES tenant(tenant_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.approval ADD CONSTRAINT request_approval_request_state_foreign_key FOREIGN KEY (state_id) REFERENCES request.state(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.approval ADD CONSTRAINT request_approval_current_handler_foreign_key FOREIGN KEY (current_handler) REFERENCES uiuser(uiuser_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.approval ADD CONSTRAINT request_approval_recent_handler_foreign_key FOREIGN KEY (recent_handler) REFERENCES uiuser(uiuser_id) ON UPDATE RESTRICT ON DELETE CASCADE;
--- request.ticket ---
ALTER TABLE request.ticket ADD CONSTRAINT request_ticket_request_state_foreign_key FOREIGN KEY (state_id) REFERENCES request.state(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.ticket ADD CONSTRAINT request_ticket_tenant_foreign_key FOREIGN KEY (tenant_id) REFERENCES tenant(tenant_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.ticket ADD CONSTRAINT request_ticket_uiuser_foreign_key FOREIGN KEY (requester_id) REFERENCES uiuser(uiuser_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.ticket ADD CONSTRAINT request_ticket_current_handler_foreign_key FOREIGN KEY (current_handler) REFERENCES uiuser(uiuser_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.ticket ADD CONSTRAINT request_ticket_recent_handler_foreign_key FOREIGN KEY (recent_handler) REFERENCES uiuser(uiuser_id) ON UPDATE RESTRICT ON DELETE CASCADE;
--- owner ---
ALTER TABLE owner ADD CONSTRAINT owner_tenant_foreign_key FOREIGN KEY (tenant_id) REFERENCES tenant(tenant_id) ON UPDATE RESTRICT ON DELETE CASCADE;
--- comment ---
ALTER TABLE request.comment ADD CONSTRAINT request_comment_uiuser_foreign_key FOREIGN KEY (creator_id) REFERENCES uiuser(uiuser_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.comment ADD CONSTRAINT request_comment_request_comment_foreign_key FOREIGN KEY (ref_id) REFERENCES request.comment(id) ON UPDATE RESTRICT ON DELETE CASCADE;
--- owner_network ---
ALTER TABLE owner_network ADD CONSTRAINT owner_network_proto_foreign_key FOREIGN KEY (ip_proto_id) REFERENCES stm_ip_proto(ip_proto_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE owner_network ADD CONSTRAINT owner_network_owner_foreign_key FOREIGN KEY (owner_id) REFERENCES owner(id) ON UPDATE RESTRICT ON DELETE CASCADE;
--- rule_owner ---
ALTER TABLE rule_owner ADD CONSTRAINT rule_owner_rule_metadata_foreign_key FOREIGN KEY (rule_metadata_id) REFERENCES rule_metadata(rule_metadata_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE rule_owner ADD CONSTRAINT rule_owner_owner_foreign_key FOREIGN KEY (owner_id) REFERENCES owner(id) ON UPDATE RESTRICT ON DELETE CASCADE;
--- reqtask_owner ---
ALTER TABLE reqtask_owner ADD CONSTRAINT reqtask_owner_request_reqtask_foreign_key FOREIGN KEY (reqtask_id) REFERENCES request.reqtask(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE reqtask_owner ADD CONSTRAINT reqtask_owner_owner_foreign_key FOREIGN KEY (owner_id) REFERENCES owner(id) ON UPDATE RESTRICT ON DELETE CASCADE;
--- ticket_comment ---
ALTER TABLE request.ticket_comment ADD CONSTRAINT request_ticket_comment_ticket_foreign_key FOREIGN KEY (ticket_id) REFERENCES request.ticket(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.ticket_comment ADD CONSTRAINT request_ticket_comment_comment_foreign_key FOREIGN KEY (comment_id) REFERENCES request.comment(id) ON UPDATE RESTRICT ON DELETE CASCADE;
--- reqtask_comment ---
ALTER TABLE request.reqtask_comment ADD CONSTRAINT request_reqtask_comment_reqtask_foreign_key FOREIGN KEY (task_id) REFERENCES request.reqtask(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.reqtask_comment ADD CONSTRAINT request_reqtask_comment_comment_foreign_key FOREIGN KEY (comment_id) REFERENCES request.comment(id) ON UPDATE RESTRICT ON DELETE CASCADE;
--- approval_comment ---
ALTER TABLE request.approval_comment ADD CONSTRAINT request_approval_comment_approval_foreign_key FOREIGN KEY (approval_id) REFERENCES request.approval(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.approval_comment ADD CONSTRAINT request_approval_comment_comment_foreign_key FOREIGN KEY (comment_id) REFERENCES request.comment(id) ON UPDATE RESTRICT ON DELETE CASCADE;
--- impltask_comment ---
ALTER TABLE request.impltask_comment ADD CONSTRAINT request_impltask_comment_impltask_foreign_key FOREIGN KEY (task_id) REFERENCES request.impltask(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.impltask_comment ADD CONSTRAINT request_impltask_comment_comment_foreign_key FOREIGN KEY (comment_id) REFERENCES request.comment(id) ON UPDATE RESTRICT ON DELETE CASCADE;
--- state_action ---
ALTER TABLE request.state_action ADD CONSTRAINT request_state_action_state_foreign_key FOREIGN KEY (state_id) REFERENCES request.state(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.state_action ADD CONSTRAINT request_state_action_action_foreign_key FOREIGN KEY (action_id) REFERENCES request.action(id) ON UPDATE RESTRICT ON DELETE CASCADE;
--- request.implelement ---
ALTER TABLE request.implelement ADD CONSTRAINT request_implelement_request_implelement_foreign_key FOREIGN KEY (original_nat_id) REFERENCES request.implelement(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.implelement ADD CONSTRAINT request_implelement_service_foreign_key FOREIGN KEY (service_id) REFERENCES service(svc_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.implelement ADD CONSTRAINT request_implelement_object_foreign_key FOREIGN KEY (network_object_id) REFERENCES object(obj_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.implelement ADD CONSTRAINT request_implelement_proto_foreign_key FOREIGN KEY (ip_proto_id) REFERENCES stm_ip_proto(ip_proto_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.implelement ADD CONSTRAINT request_implelement_request_impltask_foreign_key FOREIGN KEY (implementation_task_id) REFERENCES request.impltask(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.implelement ADD CONSTRAINT request_implelement_usr_foreign_key FOREIGN KEY (user_id) REFERENCES usr(user_id) ON UPDATE RESTRICT ON DELETE CASCADE;
--- request.impltask
ALTER TABLE request.impltask ADD CONSTRAINT request_impltask_request_reqtask_foreign_key FOREIGN KEY (reqtask_id) REFERENCES request.reqtask(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.impltask ADD CONSTRAINT request_impltask_request_state_foreign_key FOREIGN KEY (state_id) REFERENCES request.state(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.impltask ADD CONSTRAINT request_impltask_device_foreign_key FOREIGN KEY (device_id) REFERENCES device(dev_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.impltask ADD CONSTRAINT request_impltask_stm_action_foreign_key FOREIGN KEY (rule_action) REFERENCES stm_action(action_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.impltask ADD CONSTRAINT request_impltask_stm_tracking_foreign_key FOREIGN KEY (rule_tracking) REFERENCES stm_track(track_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.impltask ADD CONSTRAINT request_impltask_service_foreign_key FOREIGN KEY (svc_grp_id) REFERENCES service(svc_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.impltask ADD CONSTRAINT request_impltask_object_foreign_key FOREIGN KEY (nw_obj_grp_id) REFERENCES object(obj_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.impltask ADD CONSTRAINT request_impltask_usergrp_foreign_key FOREIGN KEY (user_grp_id) REFERENCES usr(user_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.impltask ADD CONSTRAINT request_impltask_current_handler_foreign_key FOREIGN KEY (current_handler) REFERENCES uiuser(uiuser_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.impltask ADD CONSTRAINT request_impltask_recent_handler_foreign_key FOREIGN KEY (recent_handler) REFERENCES uiuser(uiuser_id) ON UPDATE RESTRICT ON DELETE CASCADE;

--- OTHER CONSTRAINTS ---

--- DELETE ---

--- owner_network ---
ALTER TABLE owner_network DROP CONSTRAINT IF EXISTS port_in_valid_range;
--- request.reqelement ---
ALTER TABLE request.reqelement DROP CONSTRAINT IF EXISTS port_in_valid_range;
--- request.implelement ---
ALTER TABLE request.implelement DROP CONSTRAINT IF EXISTS port_in_valid_range;

--- ADD ---

--- owner_network ---
ALTER TABLE owner_network ADD CONSTRAINT port_in_valid_range CHECK (port > 0 and port <= 65535);
--- request.reqelement ---
ALTER TABLE request.reqelement ADD CONSTRAINT port_in_valid_range CHECK (port > 0 and port <= 65535);
--- request.implelement ---
ALTER TABLE request.implelement ADD CONSTRAINT port_in_valid_range CHECK (port > 0 and port <= 65535);


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

---------------------------------------------------------------------------------------
-- adding import_credential table

-- drop table if exists import_credential;

create table if not exists import_credential
(
    id SERIAL PRIMARY KEY,
    credential_name varchar NOT NULL,
    is_key_pair BOOLEAN default FALSE,
    username varchar NOT NULL,
    secret text NOT NULL,
	public_key Text
);

ALTER TABLE management ADD COLUMN IF NOT EXISTS import_credential_id int;

-- during first upgrade to 5.7.1 -- migrate credentials from management to import_credential 

DO $do$ 
DECLARE
    i_cred_number INT;
    v_cred_number_string VARCHAR;
    r_cred RECORD;
    i_cred_id INT;
BEGIN
    SELECT INTO r_cred column_name FROM information_schema.columns WHERE table_name='management' and column_name='secret';
    -- only migrate credentials if management table still contains "secret" column 
    IF FOUND THEN
        SELECT INTO i_cred_number COUNT(*) FROM import_credential;

        IF i_cred_number=0 THEN
            i_cred_number := 1;
            FOR r_cred IN SELECT DISTINCT secret, ssh_user, ssh_public_key FROM management
            LOOP
                v_cred_number_string := 'credential' || CAST (i_cred_number AS VARCHAR);
                IF NOT r_cred.secret LIKE '%BEGIN OPENSSH PRIVATE KEY%' THEN
                    INSERT INTO import_credential 
                        (credential_name, is_key_pair, username, secret) 
                        VALUES (v_cred_number_string, FALSE, r_cred.ssh_user, r_cred.secret)
                        RETURNING id INTO i_cred_id;
                    UPDATE management 
                        SET import_credential_id=i_cred_id
                        WHERE secret=r_cred.secret AND ssh_user=r_cred.ssh_user;
                ELSE
                    INSERT INTO import_credential
                        (credential_name, is_key_pair, username, secret, public_key) 
                        VALUES (v_cred_number_string, TRUE, r_cred.ssh_user, r_cred.secret, r_cred.ssh_public_key)
                        RETURNING id INTO i_cred_id;
                    UPDATE management 
                        SET import_credential_id=i_cred_id
                        WHERE secret=r_cred.secret AND ssh_user=r_cred.ssh_user; -- AND ssh_public_key=r_cred.ssh_public_key;
                END IF;
                i_cred_number := i_cred_number + 1;
            END LOOP;
        END IF;
        ALTER TABLE management DROP CONSTRAINT IF EXISTS management_import_credential_id_foreign_key;
        ALTER TABLE management ADD CONSTRAINT management_import_credential_id_foreign_key FOREIGN KEY (import_credential_id) REFERENCES import_credential(id) ON UPDATE RESTRICT ON DELETE CASCADE;
        -- and delete management columns afterwards
        -- need to remove all refs (API, etc.) first 
        ALTER TABLE management DROP COLUMN IF EXISTS ssh_public_key;
        ALTER TABLE management DROP COLUMN IF EXISTS secret;
        ALTER TABLE management DROP COLUMN IF EXISTS ssh_user;
    END IF;
END $do$;

-- Cisco Firepower Devices
insert into stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc,dev_typ_is_multi_mgmt)
 VALUES (14,'Cisco Firepower Management Center','7ff','Cisco','',true) ON CONFLICT DO NOTHING;
insert into stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc,dev_typ_is_multi_mgmt) 
 VALUES (15,'Cisco Firepower Domain','7ff','Cisco','',false) ON CONFLICT DO NOTHING;
insert into stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc,dev_typ_is_multi_mgmt) 
 VALUES (16,'Cisco Firepower Gateway','7ff','Cisco','',false) ON CONFLICT DO NOTHING;
 
