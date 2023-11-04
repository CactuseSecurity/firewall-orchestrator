insert into config (config_key, config_value, config_user) VALUES ('allowServerInConn', 'True', 0) ON CONFLICT DO NOTHING;
insert into config (config_key, config_value, config_user) VALUES ('allowServiceInConn', 'True', 0) ON CONFLICT DO NOTHING;
insert into config (config_key, config_value, config_user) VALUES ('importAppDataStartAt', '00:00:00', 0) ON CONFLICT DO NOTHING;
insert into config (config_key, config_value, config_user) VALUES ('importAppDataSleepTime', '0', 0) ON CONFLICT DO NOTHING;
insert into config (config_key, config_value, config_user) VALUES ('importSubnetDataStartAt', '00:00:00', 0) ON CONFLICT DO NOTHING;
insert into config (config_key, config_value, config_user) VALUES ('importSubnetDataSleepTime', '0', 0) ON CONFLICT DO NOTHING;
insert into config (config_key, config_value, config_user) VALUES ('importAppDataPath', '[]', 0) ON CONFLICT DO NOTHING;
insert into config (config_key, config_value, config_user) VALUES ('importSubnetDataPath', '', 0) ON CONFLICT DO NOTHING;

alter table owner add column if not exists criticality Varchar;
alter table owner add column if not exists active boolean default true;
alter table owner add column if not exists import_source Varchar;


create schema if not exists modelling;

create table if not exists modelling.area
(
 	id SERIAL PRIMARY KEY,
	name Varchar NOT NULL UNIQUE
);

create table if not exists modelling.area_subnet
(
 	id SERIAL PRIMARY KEY,
	name Varchar,
	area_id int,
	network cidr
);

create table if not exists modelling.app_server
(
 	id BIGSERIAL PRIMARY KEY,
	app_id int,
	name Varchar,
	ip cidr,
	subnet Varchar,
	import_source Varchar default 'manual', 
	is_deleted boolean default false
);

create table if not exists modelling.app_role
(
 	id SERIAL PRIMARY KEY,
	app_id int,
	id_string Varchar, -- prefix format AR... -> settings 
	name Varchar,
	comment Varchar,
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
	used_interface_id int
);

create table if not exists modelling.app_zone
(
	id SERIAL PRIMARY KEY,
	name Varchar
);

create table if not exists modelling.appserver_approle
(
    appserver_id bigint,
    approle_id int
);

create table if not exists modelling.approle_connection
(
    approle_id int,
    connection_id int,
	connection_field int -- enum src=1, dest=2, ...
);

create table if not exists modelling.appserver_connection -- (used only if settings flag is set)
(
    appserver_id bigint,
    connection_id int,
	connection_field int -- enum src=1, dest=2, ...
);

create table if not exists modelling.service
(
 	id SERIAL PRIMARY KEY,
	app_id int,
	name Varchar,
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
	comment Varchar
);

create table if not exists modelling.service_service_group
(
	service_id int,
    service_group_id int
);

create table if not exists modelling.service_group_connection
(
    service_group_id int,
	connection_id int
);

create table if not exists modelling.service_connection -- (used only if settings flag is set)
(
    service_id int,
    connection_id int
);

create table if not exists modelling.change_history
(
	id BIGSERIAL PRIMARY KEY,
	change_time Timestamp,
	app_id int,
    connection_id bigint,
	change Varchar
);


ALTER TABLE modelling.area_subnet DROP CONSTRAINT IF EXISTS modelling_area_subnet_area_foreign_key;
ALTER TABLE modelling.app_server DROP CONSTRAINT IF EXISTS modelling_app_server_owner_foreign_key;
ALTER TABLE modelling.app_role DROP CONSTRAINT IF EXISTS modelling_app_role_owner_foreign_key;
ALTER TABLE modelling.connection DROP CONSTRAINT IF EXISTS modelling_connection_owner_foreign_key;
ALTER TABLE modelling.connection DROP CONSTRAINT IF EXISTS modelling_connection_used_interface_foreign_key;
ALTER TABLE modelling.appserver_approle DROP CONSTRAINT IF EXISTS modelling_appserver_approle_appserver_foreign_key;
ALTER TABLE modelling.appserver_approle DROP CONSTRAINT IF EXISTS modelling_appserver_approle_approle_foreign_key;
ALTER TABLE modelling.approle_connection DROP CONSTRAINT IF EXISTS modelling_approle_connection_approle_foreign_key;
ALTER TABLE modelling.approle_connection DROP CONSTRAINT IF EXISTS modelling_approle_connection_connection_foreign_key;
ALTER TABLE modelling.appserver_connection DROP CONSTRAINT IF EXISTS modelling_appserver_connection_appserver_foreign_key;
ALTER TABLE modelling.appserver_connection DROP CONSTRAINT IF EXISTS modelling_appserver_connection_connection_foreign_key;
ALTER TABLE modelling.service DROP CONSTRAINT IF EXISTS modelling_service_owner_foreign_key;
ALTER TABLE modelling.service DROP CONSTRAINT IF EXISTS modelling_service_protocol_foreign_key;
ALTER TABLE modelling.service_group DROP CONSTRAINT IF EXISTS modelling_service_group_owner_foreign_key;
ALTER TABLE modelling.service_service_group DROP CONSTRAINT IF EXISTS modelling_service_service_group_service_foreign_key;
ALTER TABLE modelling.service_service_group DROP CONSTRAINT IF EXISTS modelling_service_service_group_service_group_foreign_key;
ALTER TABLE modelling.service_group_connection DROP CONSTRAINT IF EXISTS modelling_service_group_connection_service_group_foreign_key;
ALTER TABLE modelling.service_group_connection DROP CONSTRAINT IF EXISTS modelling_service_group_connection_connection_foreign_key;
ALTER TABLE modelling.service_connection DROP CONSTRAINT IF EXISTS modelling_service_connection_service_foreign_key;
ALTER TABLE modelling.service_connection DROP CONSTRAINT IF EXISTS modelling_service_connection_connection_foreign_key;
ALTER TABLE modelling.change_history DROP CONSTRAINT IF EXISTS modelling_change_history_connection_foreign_key;
ALTER TABLE modelling.change_history DROP CONSTRAINT IF EXISTS modelling_change_history_owner_foreign_key;

ALTER TABLE modelling.area_subnet ADD CONSTRAINT modelling_area_subnet_area_foreign_key FOREIGN KEY (area_id) REFERENCES modelling.area(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE modelling.app_server ADD CONSTRAINT modelling_app_server_owner_foreign_key FOREIGN KEY (app_id) REFERENCES owner(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE modelling.app_role ADD CONSTRAINT modelling_app_role_owner_foreign_key FOREIGN KEY (app_id) REFERENCES owner(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE modelling.connection ADD CONSTRAINT modelling_connection_owner_foreign_key FOREIGN KEY (app_id) REFERENCES owner(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE modelling.connection ADD CONSTRAINT modelling_connection_used_interface_foreign_key FOREIGN KEY (used_interface_id) REFERENCES modelling.connection(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE modelling.appserver_approle ADD CONSTRAINT modelling_appserver_approle_appserver_foreign_key FOREIGN KEY (appserver_id) REFERENCES modelling.app_server(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE modelling.appserver_approle ADD CONSTRAINT modelling_appserver_approle_approle_foreign_key FOREIGN KEY (approle_id) REFERENCES modelling.app_role(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE modelling.approle_connection ADD CONSTRAINT modelling_approle_connection_approle_foreign_key FOREIGN KEY (approle_id) REFERENCES modelling.app_role(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE modelling.approle_connection ADD CONSTRAINT modelling_approle_connection_connection_foreign_key FOREIGN KEY (connection_id) REFERENCES modelling.connection(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE modelling.appserver_connection ADD CONSTRAINT modelling_appserver_connection_appserver_foreign_key FOREIGN KEY (appserver_id) REFERENCES modelling.app_server(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE modelling.appserver_connection ADD CONSTRAINT modelling_appserver_connection_connection_foreign_key FOREIGN KEY (connection_id) REFERENCES modelling.connection(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE modelling.service ADD CONSTRAINT modelling_service_owner_foreign_key FOREIGN KEY (app_id) REFERENCES owner(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE modelling.service ADD CONSTRAINT modelling_service_protocol_foreign_key FOREIGN KEY (proto_id) REFERENCES stm_ip_proto(ip_proto_id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE modelling.service_group ADD CONSTRAINT modelling_service_group_owner_foreign_key FOREIGN KEY (app_id) REFERENCES owner(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE modelling.service_service_group ADD CONSTRAINT modelling_service_service_group_service_foreign_key FOREIGN KEY (service_id) REFERENCES modelling.service(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE modelling.service_service_group ADD CONSTRAINT modelling_service_service_group_service_group_foreign_key FOREIGN KEY (service_group_id) REFERENCES modelling.service_group(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE modelling.service_group_connection ADD CONSTRAINT modelling_service_group_connection_service_group_foreign_key FOREIGN KEY (service_group_id) REFERENCES modelling.service_group(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE modelling.service_group_connection ADD CONSTRAINT modelling_service_group_connection_connection_foreign_key FOREIGN KEY (connection_id) REFERENCES modelling.connection(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE modelling.service_connection ADD CONSTRAINT modelling_service_connection_service_foreign_key FOREIGN KEY (service_id) REFERENCES modelling.service(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE modelling.service_connection ADD CONSTRAINT modelling_service_connection_connection_foreign_key FOREIGN KEY (connection_id) REFERENCES modelling.connection(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE modelling.change_history ADD CONSTRAINT modelling_change_history_connection_foreign_key FOREIGN KEY (connection_id) REFERENCES modelling.connection(id) ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE modelling.change_history ADD CONSTRAINT modelling_change_history_owner_foreign_key FOREIGN KEY (app_id) REFERENCES owner(id) ON UPDATE RESTRICT ON DELETE CASCADE;

