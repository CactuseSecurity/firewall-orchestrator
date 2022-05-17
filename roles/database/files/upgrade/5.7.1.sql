-- create schema
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
    IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'task_type_enum') THEN
    CREATE TYPE task_type_enum AS ENUM ('access', 'svc_group', 'obj_group', 'rule_modify');
    END IF;
END
$$;

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'action_enum') THEN
    CREATE TYPE request.action_enum AS ENUM ('create', 'delete', 'modifiy');
    END IF;
END
$$;

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'state_enum') THEN
    CREATE TYPE request.state_enum AS ENUM ('draft', 'open', 'in progress', 'closed', 'cancelled');
    END IF;
END
$$;

-- create tables
create table if not exists request.task 
(
    id SERIAL PRIMARY KEY,
    title VARCHAR,
    ticket_id int,
    task_number int,
    state request.state_enum NOT NULL,
    task_type task_type_enum NOT NULL,
    request_action request.action_enum NOT NULL,
    rule_action int,
    rule_tracking int,
    start Timestamp,
    stop Timestamp,
    svc_grp_id int,
    nw_obj_grp_id int,
    reason text
);

create table if not exists request.element 
(
    id SERIAL PRIMARY KEY,
    request_action request.action_enum NOT NULL default 'create',
    task_id int,
    ip cidr,
    port int,
    proto int,
    network_object_id bigint,
    service_id bigint,
    field rule_field_enum NOT NULL,
    user_id bigint,
    original_nat_id int
);

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

create table if not exists request.ticket 
(
    id SERIAL PRIMARY KEY,
    title VARCHAR NOT NULL,
    date_created Timestamp NOT NULL default CURRENT_TIMESTAMP,
    date_completed Timestamp,
    state_id request.state_enum NOT NULL,
    requester_id int,
    requester_dn Varchar,
    requester_group Varchar,
    tenant_id int,
    reason text
);

create table if not exists owner
(
    id SERIAL PRIMARY KEY,
    name Varchar NOT NULL,
    dn Varchar NOT NULL,
    group_dn Varchar NOT NULL,
    is_default boolean default false,
    tenant_id int,
    recert_interval interval
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
    request_task_id int,
    owner_id int
);

create table if not exists rule_owner
(
    owner_id int,
    rule_metadata_id bigint
);

create table if not exists implementation.element
(
    id SERIAL PRIMARY KEY,
    implementation_action request.action_enum NOT NULL default 'create',
    implementation_task_id int,
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
    id SERIAL PRIMARY KEY,
    request_task_id int,
    implementation_task_number int,
    implementation_state request.state_enum NOT NULL default 'open',
    device_id int,
    implementation_action request.action_enum NOT NULL,
    rule_action int,
    rule_tracking int,
    start timestamp,
    stop timestamp,
    svc_grp_id int,
    nw_obj_grp_id int
);

--- FOREIGN KEYS ---

--- Drop ---

--- request.task ---
ALTER TABLE request.task DROP CONSTRAINT IF EXISTS request_task_request_ticket_foreign_key;
ALTER TABLE request.task DROP CONSTRAINT IF EXISTS request_task_stm_action_foreign_key;
ALTER TABLE request.task DROP CONSTRAINT IF EXISTS request_task_stm_track_foreign_key;
ALTER TABLE request.task DROP CONSTRAINT IF EXISTS request_task_service_foreign_key;
ALTER TABLE request.task DROP CONSTRAINT IF EXISTS request_task_object_foreign_key;
--- request.element ---
ALTER TABLE request.element DROP CONSTRAINT IF EXISTS request_element_request_task_foreign_key;
ALTER TABLE request.element DROP CONSTRAINT IF EXISTS request_element_service_foreign_key;
ALTER TABLE request.element DROP CONSTRAINT IF EXISTS request_element_object_foreign_key;
ALTER TABLE request.element DROP CONSTRAINT IF EXISTS request_element_request_element_foreign_key;
ALTER TABLE request.element DROP CONSTRAINT IF EXISTS request_element_usr_foreign_key;
--- request.approval ---
ALTER TABLE request.approval DROP CONSTRAINT IF EXISTS request_approval_request_task_foreign_key;
ALTER TABLE request.approval DROP CONSTRAINT IF EXISTS request_approval_tenant_foreign_key;
--- request.ticket ---
ALTER TABLE request.ticket DROP CONSTRAINT IF EXISTS request_ticket_tenant_foreign_key;
ALTER TABLE request.ticket DROP CONSTRAINT IF EXISTS request_ticket_uiuser_foreign_key;
--- owner ---
ALTER TABLE owner DROP CONSTRAINT IF EXISTS owner_tenant_foreign_key;
--- owner_network ---
ALTER TABLE owner_network DROP CONSTRAINT IF EXISTS owner_network_proto_foreign_key;
ALTER TABLE owner_network DROP CONSTRAINT IF EXISTS owner_network_owner_foreign_key;
--- rule_owner ---
ALTER TABLE rule_owner DROP CONSTRAINT IF EXISTS rule_owner_rule_metadata_foreign_key;
ALTER TABLE rule_owner DROP CONSTRAINT IF EXISTS rule_owner_owner_foreign_key;
--- request_owner ---
ALTER TABLE request_owner DROP CONSTRAINT IF EXISTS request_owner_request_task_foreign_key;
ALTER TABLE request_owner DROP CONSTRAINT IF EXISTS request_owner_owner_foreign_key;
--- implemantation.element ---
ALTER TABLE implementation.element DROP CONSTRAINT IF EXISTS implementation_element_implementation_element_foreign_key;
ALTER TABLE implementation.element DROP CONSTRAINT IF EXISTS implementation_element_service_foreign_key;
ALTER TABLE implementation.element DROP CONSTRAINT IF EXISTS implementation_element_object_foreign_key;
ALTER TABLE implementation.element DROP CONSTRAINT IF EXISTS implementation_element_proto_foreign_key;
ALTER TABLE implementation.element DROP CONSTRAINT IF EXISTS implementation_element_implementation_task_foreign_key;
ALTER TABLE implementation.element DROP CONSTRAINT IF EXISTS implementation_element_usr_foreign_key;
--- implementation.task
ALTER TABLE implementation.task DROP CONSTRAINT IF EXISTS implementation_task_request_task_foreign_key;
ALTER TABLE implementation.task DROP CONSTRAINT IF EXISTS implementation_task_device_foreign_key;
ALTER TABLE implementation.task DROP CONSTRAINT IF EXISTS implementation_task_stm_action_foreign_key;
ALTER TABLE implementation.task DROP CONSTRAINT IF EXISTS implementation_task_stm_tracking_foreign_key;
ALTER TABLE implementation.task DROP CONSTRAINT IF EXISTS implementation_task_service_foreign_key;
ALTER TABLE implementation.task DROP CONSTRAINT IF EXISTS implementation_task_object_foreign_key;

--- ADD ---

--- request.task ---
ALTER TABLE request.task ADD CONSTRAINT request_task_request_ticket_foreign_key FOREIGN KEY (ticket_id) REFERENCES request.ticket(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.task ADD CONSTRAINT request_task_stm_action_foreign_key FOREIGN KEY (rule_action) REFERENCES stm_action(action_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.task ADD CONSTRAINT request_task_stm_track_foreign_key FOREIGN KEY (rule_tracking) REFERENCES stm_track(track_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.task ADD CONSTRAINT request_task_service_foreign_key FOREIGN KEY (svc_grp_id) REFERENCES service(svc_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.task ADD CONSTRAINT request_task_object_foreign_key FOREIGN KEY (nw_obj_grp_id) REFERENCES object(obj_id) ON UPDATE RESTRICT ON DELETE CASCADE;
--- request.element ---
ALTER TABLE request.element ADD CONSTRAINT request_element_request_task_foreign_key FOREIGN KEY (task_id) REFERENCES request.task(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.element ADD CONSTRAINT request_element_service_foreign_key FOREIGN KEY (service_id) REFERENCES service(svc_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.element ADD CONSTRAINT request_element_object_foreign_key FOREIGN KEY (network_object_id) REFERENCES object(obj_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.element ADD CONSTRAINT request_element_request_element_foreign_key FOREIGN KEY (original_nat_id) REFERENCES request.element(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.element ADD CONSTRAINT request_element_usr_foreign_key FOREIGN KEY (user_id) REFERENCES usr(user_id) ON UPDATE RESTRICT ON DELETE CASCADE;
--- request.approval ---
ALTER TABLE request.approval ADD CONSTRAINT request_approval_request_task_foreign_key FOREIGN KEY (task_id) REFERENCES request.task(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.approval ADD CONSTRAINT request_approval_tenant_foreign_key FOREIGN KEY (tenant_id) REFERENCES tenant(tenant_id) ON UPDATE RESTRICT ON DELETE CASCADE;
--- request.ticket ---
ALTER TABLE request.ticket ADD CONSTRAINT request_ticket_tenant_foreign_key FOREIGN KEY (tenant_id) REFERENCES tenant(tenant_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request.ticket ADD CONSTRAINT request_ticket_uiuser_foreign_key FOREIGN KEY (requester_id) REFERENCES uiuser(uiuser_id) ON UPDATE RESTRICT ON DELETE CASCADE;
--- owner ---
ALTER TABLE owner ADD CONSTRAINT owner_tenant_foreign_key FOREIGN KEY (tenant_id) REFERENCES tenant(tenant_id) ON UPDATE RESTRICT ON DELETE CASCADE;
--- owner_network ---
ALTER TABLE owner_network ADD CONSTRAINT owner_network_proto_foreign_key FOREIGN KEY (ip_proto_id) REFERENCES stm_ip_proto(ip_proto_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE owner_network ADD CONSTRAINT owner_network_owner_foreign_key FOREIGN KEY (owner_id) REFERENCES owner(id) ON UPDATE RESTRICT ON DELETE CASCADE;
--- rule_owner ---
ALTER TABLE rule_owner ADD CONSTRAINT rule_owner_rule_metadata_foreign_key FOREIGN KEY (rule_metadata_id) REFERENCES rule_metadata(rule_metadata_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE rule_owner ADD CONSTRAINT rule_owner_owner_foreign_key FOREIGN KEY (owner_id) REFERENCES owner(id) ON UPDATE RESTRICT ON DELETE CASCADE;
--- request_owner ---
ALTER TABLE request_owner ADD CONSTRAINT request_owner_request_task_foreign_key FOREIGN KEY (request_task_id) REFERENCES request.task(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE request_owner ADD CONSTRAINT request_owner_owner_foreign_key FOREIGN KEY (owner_id) REFERENCES owner(id) ON UPDATE RESTRICT ON DELETE CASCADE;
--- implemantation.element ---
ALTER TABLE implementation.element ADD CONSTRAINT implementation_element_implementation_element_foreign_key FOREIGN KEY (original_nat_id) REFERENCES implementation.element(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE implementation.element ADD CONSTRAINT implementation_element_service_foreign_key FOREIGN KEY (service_id) REFERENCES service(svc_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE implementation.element ADD CONSTRAINT implementation_element_object_foreign_key FOREIGN KEY (network_object_id) REFERENCES object(obj_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE implementation.element ADD CONSTRAINT implementation_element_proto_foreign_key FOREIGN KEY (ip_proto_id) REFERENCES stm_ip_proto(ip_proto_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE implementation.element ADD CONSTRAINT implementation_element_implementation_task_foreign_key FOREIGN KEY (implementation_task_id) REFERENCES implementation.task(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE implementation.element ADD CONSTRAINT implementation_element_usr_foreign_key FOREIGN KEY (user_id) REFERENCES usr(user_id) ON UPDATE RESTRICT ON DELETE CASCADE;
--- implementation.task
ALTER TABLE implementation.task ADD CONSTRAINT implementation_task_request_task_foreign_key FOREIGN KEY (request_task_id) REFERENCES request.task(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE implementation.task ADD CONSTRAINT implementation_task_device_foreign_key FOREIGN KEY (device_id) REFERENCES device(dev_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE implementation.task ADD CONSTRAINT implementation_task_stm_action_foreign_key FOREIGN KEY (rule_action) REFERENCES stm_action(action_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE implementation.task ADD CONSTRAINT implementation_task_stm_tracking_foreign_key FOREIGN KEY (rule_tracking) REFERENCES stm_track(track_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE implementation.task ADD CONSTRAINT implementation_task_service_foreign_key FOREIGN KEY (svc_grp_id) REFERENCES service(svc_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE implementation.task ADD CONSTRAINT implementation_task_object_foreign_key FOREIGN KEY (nw_obj_grp_id) REFERENCES object(obj_id) ON UPDATE RESTRICT ON DELETE CASCADE;

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
insert into config (config_key, config_value, config_user) VALUES ('importFwProxy', '', 0) ON CONFLICT DO NOTHING;
insert into config (config_key, config_value, config_user) VALUES ('sessionTimeout', '240', 0) ON CONFLICT DO NOTHING;
-- insert into config (config_key, config_value, config_user) VALUES ('maxMessages', '3', 0) ON CONFLICT DO NOTHING;
