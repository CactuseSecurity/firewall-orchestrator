
-- owner -------------------------------------------------------

create table owner
(
    id SERIAL PRIMARY KEY,
    name Varchar NOT NULL,
    -- responsibles stored in owner_responsible table
    is_default boolean default false,
    tenant_id int,
    recert_interval int,
    app_id_external varchar UNIQUE,
    last_recert_check Timestamp,
    recert_check_params Varchar,
	criticality Varchar,
	owner_lifecycle_state_id int,
	active boolean default true,
	import_source Varchar,
	common_service_possible boolean default false,
	last_recertified Timestamp,
	last_recertifier int,
	last_recertifier_dn Varchar,
	next_recert_date Timestamp,
    recert_active boolean default false,
    decomm_date Timestamp,
    additional_info jsonb
);

create table owner_responsible
(
    id SERIAL PRIMARY KEY,
    owner_id int NOT NULL,
    dn Varchar NOT NULL,
    responsible_type int NOT NULL
);

create table owner_responsible_type
(
    id SERIAL PRIMARY KEY,
    name Varchar NOT NULL,
    active boolean default true,
    sort_order int default 0,
    allow_modelling boolean default false,
    allow_recertification boolean default false
);

CREATE TABLE owner_lifecycle_state (
    id SERIAL PRIMARY KEY,
    name Varchar NOT NULL,
    active_state boolean NOT NULL default true
);

create table owner_network
(
    id BIGSERIAL PRIMARY KEY,
    owner_id int,
	name Varchar,
    ip cidr NOT NULL,
    ip_end cidr NOT NULL,
    port int,
    ip_proto_id int,
	nw_type int,
	import_source Varchar default 'manual', 
	is_deleted boolean default false,
	custom_type int
);

create table reqtask_owner
(
    reqtask_id bigint,
    owner_id int
);

create table rule_owner
(
    owner_id int,
    rule_metadata_id bigint,
    rule_id bigint NOT NULL,
    created bigint NOT NULL,
    removed bigint,
    owner_mapping_source_id smallint NOT NULL,
    primary key (rule_id, owner_id, created)
);

create table recertification
(
	id BIGSERIAL PRIMARY KEY,
    rule_metadata_id bigint NOT NULL,
	rule_id bigint NOT NULL,
	ip_match varchar,
    owner_id int,
	user_dn varchar,
	recertified boolean default false,
	recert_date Timestamp,
	comment varchar,
	next_recert_date Timestamp,
	owner_recert_id bigint
);

create table owner_recertification
(
	id BIGSERIAL PRIMARY KEY,
    owner_id int NOT NULL,
	user_dn varchar,
	recertified boolean default false,
	recert_date Timestamp,
	comment varchar,
	next_recert_date Timestamp,
    report_id bigint
);

create table owner_ticket
(
    owner_id int,
    ticket_id bigint
);

create table ext_request
(
	id BIGSERIAL PRIMARY KEY,
    owner_id int,
    ticket_id bigint,
	task_number int,
	ext_ticket_system varchar,
	ext_request_type varchar,
	ext_request_content varchar,
	ext_query_variables varchar,
	ext_request_state varchar,
	ext_ticket_id varchar,
	last_creation_response varchar,
	last_processing_response varchar,
	create_date Timestamp default now(),
	finish_date Timestamp,
	wait_cycles int default 0,
	attempts int default 0,
	locked boolean default false
);
