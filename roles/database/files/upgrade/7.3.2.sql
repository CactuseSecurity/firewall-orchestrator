insert into config (config_key, config_value, config_user) VALUES ('allowServerInConn', 'True', 0) ON CONFLICT DO NOTHING;
insert into config (config_key, config_value, config_user) VALUES ('allowServiceInConn', 'True', 0) ON CONFLICT DO NOTHING;
insert into config (config_key, config_value, config_user) VALUES ('importAppDataStartAt', '00:00:00', 0) ON CONFLICT DO NOTHING;
insert into config (config_key, config_value, config_user) VALUES ('importAppDataSleepTime', '0', 0) ON CONFLICT DO NOTHING;
insert into config (config_key, config_value, config_user) VALUES ('importSubnetDataStartAt', '00:00:00', 0) ON CONFLICT DO NOTHING;
insert into config (config_key, config_value, config_user) VALUES ('importSubnetDataSleepTime', '0', 0) ON CONFLICT DO NOTHING;
insert into config (config_key, config_value, config_user) VALUES ('importAppDataPath', '[]', 0) ON CONFLICT DO NOTHING;
insert into config (config_key, config_value, config_user) VALUES ('importSubnetDataPath', '[]', 0) ON CONFLICT DO NOTHING;
insert into config (config_key, config_value, config_user) VALUES ('modNamingConvention', '{"networkAreaRequired":false,"fixedPartLength":0,"freePartLength":0,"networkAreaPattern":"","appRolePattern":""}', 0) ON CONFLICT DO NOTHING;

alter table owner add column if not exists criticality Varchar;
alter table owner add column if not exists active boolean default true;
alter table owner add column if not exists import_source Varchar;

alter table owner_network alter column id type bigint;
alter table owner_network add column if not exists name Varchar;
alter table owner_network add column if not exists nw_type int;
alter table owner_network add column if not exists import_source Varchar default 'manual';
alter table owner_network add column if not exists is_deleted boolean default false;

-- temp
-- ALTER TABLE modelling.nwobject DROP CONSTRAINT IF EXISTS modelling_nwobject_owner_foreign_key;
-- drop table if exists modelling.nwobject;


create schema if not exists modelling;

create table if not exists modelling.nwgroup
(
 	id BIGSERIAL PRIMARY KEY,
	app_id int,
	id_string Varchar,
	name Varchar,
	comment Varchar,
	group_type int,
	is_deleted boolean default false,
	creator Varchar,
	creation_date timestamp default now()
);

create table if not exists modelling.connection
(
 	id SERIAL PRIMARY KEY,
	app_id int,
	name Varchar,
	reason Text,
	is_interface boolean default false,
	used_interface_id int,
	common_service boolean default false,
	creator Varchar,
	creation_date timestamp default now()
);

create table if not exists modelling.selected_objects
(
	app_id int,
	nwgroup_id bigint,
	primary key (app_id, nwgroup_id)
);

create table if not exists modelling.selected_connections
(
	app_id int,
	connection_id int,
	primary key (app_id, connection_id)
);

create table if not exists modelling.nwobject_nwgroup
(
    nwobject_id bigint,
    nwgroup_id bigint,
	primary key (nwobject_id, nwgroup_id)
);

create table if not exists modelling.nwgroup_connection
(
    nwgroup_id bigint,
    connection_id int,
	connection_field int, -- enum src=1, dest=2, ...
	primary key (nwgroup_id, connection_id, connection_field)
);

create table if not exists modelling.nwobject_connection -- (used only if settings flag is set)
(
    nwobject_id bigint,
    connection_id int,
	connection_field int, -- enum src=1, dest=2, ...
	primary key (nwobject_id, connection_id, connection_field)
);

create table if not exists modelling.service
(
 	id SERIAL PRIMARY KEY,
	app_id int,
	name Varchar,
	is_global boolean default false,
	port int,
	port_end int,
	proto_id int
);

create table if not exists modelling.service_group
(
	id SERIAL PRIMARY KEY,
	app_id int,
	name Varchar,
	is_global boolean default false,
	comment Varchar,
	creator Varchar,
	creation_date timestamp default now()
);

create table if not exists modelling.service_service_group
(
	service_id int,
    service_group_id int,
	primary key (service_id, service_group_id)
);

create table if not exists modelling.service_group_connection
(
    service_group_id int,
	connection_id int,
	primary key (service_group_id, connection_id)
);

create table if not exists modelling.service_connection -- (used only if settings flag is set)
(
    service_id int,
    connection_id int,
	primary key (service_id, connection_id)
);

create table if not exists modelling.change_history
(
	id BIGSERIAL PRIMARY KEY,
	app_id int,
	change_type int,
	object_type int,
    object_id bigint,
	change_text Varchar,
	changer Varchar,
	change_time Timestamp default now()
);


ALTER TABLE modelling.nwgroup DROP CONSTRAINT IF EXISTS modelling_nwgroup_owner_foreign_key;
ALTER TABLE modelling.connection DROP CONSTRAINT IF EXISTS modelling_connection_owner_foreign_key;
ALTER TABLE modelling.connection DROP CONSTRAINT IF EXISTS modelling_connection_used_interface_foreign_key;
ALTER TABLE modelling.nwobject_nwgroup DROP CONSTRAINT IF EXISTS modelling_nwobject_nwgroup_nwobject_foreign_key;
ALTER TABLE modelling.nwobject_nwgroup DROP CONSTRAINT IF EXISTS modelling_nwobject_nwgroup_nwgroup_foreign_key;
ALTER TABLE modelling.nwgroup_connection DROP CONSTRAINT IF EXISTS modelling_nwgroup_connection_nwgroup_foreign_key;
ALTER TABLE modelling.nwgroup_connection DROP CONSTRAINT IF EXISTS modelling_nwgroup_connection_connection_foreign_key;
ALTER TABLE modelling.nwobject_connection DROP CONSTRAINT IF EXISTS modelling_nwobject_connection_nwobject_foreign_key;
ALTER TABLE modelling.nwobject_connection DROP CONSTRAINT IF EXISTS modelling_nwobject_connection_connection_foreign_key;
ALTER TABLE modelling.service DROP CONSTRAINT IF EXISTS modelling_service_owner_foreign_key;
ALTER TABLE modelling.service DROP CONSTRAINT IF EXISTS modelling_service_protocol_foreign_key;
ALTER TABLE modelling.service_group DROP CONSTRAINT IF EXISTS modelling_service_group_owner_foreign_key;
ALTER TABLE modelling.service_service_group DROP CONSTRAINT IF EXISTS modelling_service_service_group_service_foreign_key;
ALTER TABLE modelling.service_service_group DROP CONSTRAINT IF EXISTS modelling_service_service_group_service_group_foreign_key;
ALTER TABLE modelling.service_group_connection DROP CONSTRAINT IF EXISTS modelling_service_group_connection_service_group_foreign_key;
ALTER TABLE modelling.service_group_connection DROP CONSTRAINT IF EXISTS modelling_service_group_connection_connection_foreign_key;
ALTER TABLE modelling.service_connection DROP CONSTRAINT IF EXISTS modelling_service_connection_service_foreign_key;
ALTER TABLE modelling.service_connection DROP CONSTRAINT IF EXISTS modelling_service_connection_connection_foreign_key;
ALTER TABLE modelling.change_history DROP CONSTRAINT IF EXISTS modelling_change_history_owner_foreign_key;
ALTER TABLE modelling.selected_objects DROP CONSTRAINT IF EXISTS modelling_selected_objects_owner_foreign_key;
ALTER TABLE modelling.selected_objects DROP CONSTRAINT IF EXISTS modelling_selected_objects_nwgroup_foreign_key;
ALTER TABLE modelling.selected_connections DROP CONSTRAINT IF EXISTS modelling_selected_connections_owner_foreign_key;
ALTER TABLE modelling.selected_connections DROP CONSTRAINT IF EXISTS modelling_selected_connections_connection_foreign_key;

ALTER TABLE modelling.nwgroup ADD CONSTRAINT modelling_nwgroup_owner_foreign_key FOREIGN KEY (app_id) REFERENCES owner(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE modelling.connection ADD CONSTRAINT modelling_connection_owner_foreign_key FOREIGN KEY (app_id) REFERENCES owner(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE modelling.connection ADD CONSTRAINT modelling_connection_used_interface_foreign_key FOREIGN KEY (used_interface_id) REFERENCES modelling.connection(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE modelling.nwobject_nwgroup ADD CONSTRAINT modelling_nwobject_nwgroup_nwobject_foreign_key FOREIGN KEY (nwobject_id) REFERENCES owner_network(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE modelling.nwobject_nwgroup ADD CONSTRAINT modelling_nwobject_nwgroup_nwgroup_foreign_key FOREIGN KEY (nwgroup_id) REFERENCES modelling.nwgroup(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE modelling.nwgroup_connection ADD CONSTRAINT modelling_nwgroup_connection_nwgroup_foreign_key FOREIGN KEY (nwgroup_id) REFERENCES modelling.nwgroup(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE modelling.nwgroup_connection ADD CONSTRAINT modelling_nwgroup_connection_connection_foreign_key FOREIGN KEY (connection_id) REFERENCES modelling.connection(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE modelling.nwobject_connection ADD CONSTRAINT modelling_nwobject_connection_nwobject_foreign_key FOREIGN KEY (nwobject_id) REFERENCES owner_network(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE modelling.nwobject_connection ADD CONSTRAINT modelling_nwobject_connection_connection_foreign_key FOREIGN KEY (connection_id) REFERENCES modelling.connection(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE modelling.service ADD CONSTRAINT modelling_service_owner_foreign_key FOREIGN KEY (app_id) REFERENCES owner(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE modelling.service ADD CONSTRAINT modelling_service_protocol_foreign_key FOREIGN KEY (proto_id) REFERENCES stm_ip_proto(ip_proto_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE modelling.service_group ADD CONSTRAINT modelling_service_group_owner_foreign_key FOREIGN KEY (app_id) REFERENCES owner(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE modelling.service_service_group ADD CONSTRAINT modelling_service_service_group_service_foreign_key FOREIGN KEY (service_id) REFERENCES modelling.service(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE modelling.service_service_group ADD CONSTRAINT modelling_service_service_group_service_group_foreign_key FOREIGN KEY (service_group_id) REFERENCES modelling.service_group(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE modelling.service_group_connection ADD CONSTRAINT modelling_service_group_connection_service_group_foreign_key FOREIGN KEY (service_group_id) REFERENCES modelling.service_group(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE modelling.service_group_connection ADD CONSTRAINT modelling_service_group_connection_connection_foreign_key FOREIGN KEY (connection_id) REFERENCES modelling.connection(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE modelling.service_connection ADD CONSTRAINT modelling_service_connection_service_foreign_key FOREIGN KEY (service_id) REFERENCES modelling.service(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE modelling.service_connection ADD CONSTRAINT modelling_service_connection_connection_foreign_key FOREIGN KEY (connection_id) REFERENCES modelling.connection(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE modelling.change_history ADD CONSTRAINT modelling_change_history_owner_foreign_key FOREIGN KEY (app_id) REFERENCES owner(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE modelling.selected_objects ADD CONSTRAINT modelling_selected_objects_owner_foreign_key FOREIGN KEY (app_id) REFERENCES owner(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE modelling.selected_objects ADD CONSTRAINT modelling_selected_objects_nwgroup_foreign_key FOREIGN KEY (nwgroup_id) REFERENCES modelling.nwgroup(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE modelling.selected_connections ADD CONSTRAINT modelling_selected_connections_owner_foreign_key FOREIGN KEY (app_id) REFERENCES owner(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE modelling.selected_connections ADD CONSTRAINT modelling_selected_connections_connection_foreign_key FOREIGN KEY (connection_id) REFERENCES modelling.connection(id) ON UPDATE RESTRICT ON DELETE CASCADE;

