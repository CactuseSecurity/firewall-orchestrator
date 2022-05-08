-- create schema
create schema if not exists request;
create schema if not exists implementation;

CREATE TYPE if not exists rule_filed_enum AS ENUM ('source', 'destination', 'service');
CREATE TYPE if not exists request.state_enum AS ENUM ('draft', 'open', 'in progress', 'closed', 'cancelled');
CREATE TYPE if not exists request.action_enum AS ENUM ('create', 'delete', 'modifiy');
CREATE TYPE if not exists task_type_enum AS ENUM ('access', 'svc_group', 'obj_group', 'rule_modify');

-- create table
create table if not exists request.task 
(
    id SERIAL PRIMARY KEY,
    title VARCHAR,
    ticket_id int,
    task_number int,
    state request.state_enum not null,
    task_type task_type_enum,
    request_action request.action_enum,
    rule_action int,
    tracking int,
    start Timestamp,
    stop Timestamp,
    group_name VARCHAR,
    svc_grp_id int,
    nw_obj_grp_id int,
    reason text
);

-- create table
create table if not exists request.element 
(
    id SERIAL PRIMARY KEY,
    request_action request.action_enum default 'create',
    task_id int,
    ip cidr,
    port,
    proto int,
    network_object_id bigint,
    service_id bigint,
    field rule_filed_enum,
    user_id bigint
);

-- create table
create table if not exists request.approval 
(
    id SERIAL PRIMARY KEY,
    task_id int,
    date_opened Timestamp NOT NULL default CURRENT_TIMESTAMP,
    approver_group Varchar,
    approval_date Timestamp,
    approver Varchar,
    tenant_id int,
    comment text
);

-- create table
create table if not exists request.ticket 
(
    id SERIAL PRIMARY KEY,
    title int,
    date_created Timestamp NOT NULL default CURRENT_TIMESTAMP,
    date_completed Timestamp,
    state_id request.state_enum NOT NULL,
    requester Varchar NOT NULL,
    requester_group Varchar,
    tenant_id int,
    reason text
);

create table if not exists owner
(
    id SERIAL PRIMARY KEY,
    name Varchar NOT NULL,
    dn Varchar NOT NULL,
    group_dn Varchar,
    is_default boolean default false,
    tenant_id int,
    recert_interval timespan
);

create unique index if not exists only_one_default_owner on owner(is_default) 
where is_default = true;

create table if not exists owner_network
(
    id SERIAL PRIMARY KEY,
    owner_id int,
    ip cidr NOT NULL,
    port int,
    ip_proto_id int,
    FOREIGN KEY owner_id REFERENCES owner(id),
    FOREIGN KEY ip_proto_id references stm_ip_proto(ip_proto_id)
);

create table if not exists request_owner
(
    request_task_id int,
    owner_id int,
    FOREIGN KEY request_task_id REFERENCES request.task(id),
    FOREIGN KEY owner_id REFERENCES owner(id)
);

create table if not exists rule_owner
(
    owner_id int
    rule_metadata_rule_uid Varchar,
    rule_metadata_dev_id int,
);

create table if not exists implementation.element
(
    id SERIAL PRIMARY KEY,
    implementation_action request.action_enum,
    implementation_task_id int,
    ip cidr,
    port int,
    proto int,
    network_id bigint,
    service_id bigint,
    field rule_field_enum,
    user_id bigint,
    orginal_nat_id int
);

create table if not exists implementation.task
(
    id SERIAL PRIMARY KEY,
    request_task_id int,
    implementation_task_number int,
    implementation_state request.state_enum default 'open'
    gateway int,
    implementation_action int,
    rule_action int,
    rule_tracking int,
    start timestamp,
    stop timestamp,
    svc_grp_id int,
    nw_grp_id int
);


--- FOREIGN KEYS ---

--- request.task ---
ALTER TABLE request.task ADD CONSTRAINT request_task_request_ticket_foreign_key FOREIGN KEY ticket_id REFERENCES request.ticket(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.task ADD CONSTRAINT request_task_stm_action_foreign_key FOREIGN KEY rule_action REFERENCES stm_action(action_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.task ADD CONSTRAINT request_task_stm_track_foreign_key FOREIGN KEY rule_tracking REFERENCES stm_track(track_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.task ADD CONSTRAINT request_task_service_foreign_key FOREIGN KEY service_id REFERENCES service(svc_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.task ADD CONSTRAINT request_task_object_foreign_key FOREIGN KEY network_object_id REFERENCES object(obj_id) ON UPDATE RESTRICT ON DELETE CASCADE;
--- request.element ---
ALTER TABLE request.element ADD CONSTRAINT request_element_request_task_foreign_key FOREIGN KEY task_id REFERENCES request.task(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.element ADD CONSTRAINT request_element_service_foreign_key FOREIGN KEY svc_grp_id REFERENCES service(svc_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.element ADD CONSTRAINT request_element_object_foreign_key FOREIGN KEY nw_obj_grp_id REFERENCES object(obj_id) ON UPDATE RESTRICT ON DELETE CASCADE;
--- request.approval ---
ALTER TABLE request.approval ADD CONSTRAINT request_approval_request_task_foreign_key FOREIGN KEY task_id REFERENCES request.task(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.approval ADD CONSTRAINT request_approval_tenant_foreign_key FOREIGN KEY tenant_id REFERENCES tenant(tenant_id) ON UPDATE RESTRICT ON DELETE CASCADE;
--- request.ticket ---
ALTER TABLE request.ticket ADD CONSTRAINT request_ticket_tenant_foreign_key FOREIGN KEY tenant_id REFERENCES tenant(tenant_id) ON UPDATE RESTRICT ON DELETE CASCADE;
--- owner ---
ALTER TABLE owner ADD CONSTRAINT owner_tenant_foreign_key FOREIGN KEY tenant_id REFERENCES tenant(tenant_id) ON UPDATE RESTRICT ON DELETE CASCADE;
--- owner_network ---
ALTER TABLE owner_network ADD CONSTRAINT owner_network_ip_proto_foreign_key FOREIGN KEY ip_proto_id REFERENCES stm_ip_proto(ip_proto_id) ON UPDATE RESTRICT ON DELETE CASCADE;
--- rule_owner ---
ALTER TABLE rule_owner ADD CONSTRAINT rule_owner_rule_metadata_foreign_key FOREIGN KEY rule_metadata_id REFERENCES rule_metadata(rule_metadata_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE rule_owner ADD CONSTRAINT rule_owner_owner_foreign_key FOREIGN KEY owner_id REFERENCES owner(id) ON UPDATE RESTRICT ON DELETE CASCADE;
--- request_owner ---
ALTER TABLE request_owner ADD CONSTRAINT request_owner_request_task_foreign_key FOREIGN KEY request_task_id REFERENCES request.task(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request_owner ADD CONSTRAINT request_owner_owner_foreign_key FOREIGN KEY owner_id REFERENCES owner(id) ON UPDATE RESTRICT ON DELETE CASCADE;
--- implemantation.element ---
ALTER TABLE implementation.element ADD CONSTRAINT implementation_element_user_foreign_key FOREIGN KEY request_task_id REFERENCES request.task(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE implementation.element ADD CONSTRAINT implementation_element_implementation_element_foreign_key FOREIGN KEY original_nat_id REFERENCES implementation.element(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE implementation.element ADD CONSTRAINT implementation_element_service_foreign_key FOREIGN KEY service_id REFERENCES service(svc_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE implementation.element ADD CONSTRAINT implementation_element_object_foreign_key FOREIGN KEY network_object_id REFERENCES object(obj_id) ON UPDATE RESTRICT ON DELETE CASCADE;

--- OTHER CONSTRAINTS ---

--- owner_network ---
ALTER TABLE owner_network ADD CONSTRAINT port_in_valid_range CHECK port > 0 and port <= 65535;
--- request.element ---
ALTER TABLE request.element ADD CONSTRAINT port_in_valid_range CHECK port > 0 and port <= 65535;

