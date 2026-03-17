
-- workflow -------------------------------------------------------

-- create schema
create schema request;

CREATE TYPE rule_field_enum AS ENUM ('source', 'destination', 'service', 'rule', 'modelled_source', 'modelled_destination');
CREATE TYPE action_enum AS ENUM ('create', 'delete', 'modify', 'unchanged', 'addAfterCreation');

-- create tables
create table request.reqtask 
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
	devices varchar,
	additional_info varchar,
	mgm_id int
);

create table request.reqelement 
(
    id BIGSERIAL PRIMARY KEY,
    request_action action_enum NOT NULL default 'create',
    task_id bigint,
    ip cidr,
	ip_end cidr,
    port int,
	port_end int,
    ip_proto_id int,
    network_object_id bigint,
    service_id bigint,
    field rule_field_enum NOT NULL,
    user_id bigint,
    original_nat_id bigint,
	device_id int,
	rule_uid varchar,
	group_name varchar,
	name varchar
);

create table request.approval 
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

create table request.ticket 
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

create table request.comment 
(
    id BIGSERIAL PRIMARY KEY,
    ref_id bigint,
	scope varchar,
	creation_date Timestamp,
	creator_id int,
	comment_text varchar
);

create table request.ticket_comment
(
    ticket_id bigint,
    comment_id bigint
);

create table request.reqtask_comment
(
    task_id bigint,
    comment_id bigint
);

create table request.approval_comment
(
    approval_id bigint,
    comment_id bigint
);

create table request.impltask_comment
(
    task_id bigint,
    comment_id bigint
);

create table request.state
(
    id Integer NOT NULL UNIQUE PRIMARY KEY,
    name Varchar NOT NULL
);

create table request.ext_state
(
    id SERIAL PRIMARY KEY,
    name Varchar NOT NULL,
	state_id Integer
);

create table request.action
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

create table request.state_action
(
    state_id int,
    action_id int
);

create table request.implelement
(
    id BIGSERIAL PRIMARY KEY,
    implementation_action action_enum NOT NULL default 'create',
    implementation_task_id bigint,
    ip cidr,
	ip_end cidr,
    port int,
	port_end int,
    ip_proto_id int,
    network_object_id bigint,
    service_id bigint,
    field rule_field_enum NOT NULL,
    user_id bigint,
    original_nat_id bigint,
	rule_uid varchar,
	group_name varchar,
	name varchar
);

create table request.impltask
(
    id BIGSERIAL PRIMARY KEY,
	title VARCHAR,
    reqtask_id bigint,
    task_number int,
    state_id int NOT NULL,
	task_type VARCHAR NOT NULL,
    device_id int,
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

