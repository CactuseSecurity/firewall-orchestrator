
--- Network modelling ---
create schema modelling;

create table modelling.nwgroup
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

create table modelling.connection
(
 	id SERIAL PRIMARY KEY,
	app_id int,
	proposed_app_id int,
	name Varchar,
	reason Text,
	is_interface boolean default false,
	used_interface_id int,
	is_requested boolean default false,
	ticket_id bigint,
	common_service boolean default false,
	is_published boolean default false,
	creator Varchar,
	creation_date timestamp default now(),
	conn_prop Varchar,
	extra_params Varchar,
	requested_on_fw boolean default false,
	removed boolean default false,
	removal_date timestamp,
	interface_permission Varchar
);

create table modelling.permitted_owners
(
	connection_id int,
	app_id int,
	primary key (connection_id, app_id)
);

create table modelling.selected_objects
(
	app_id int,
	nwgroup_id bigint,
	primary key (app_id, nwgroup_id)
);

create table modelling.selected_connections
(
	app_id int,
	connection_id int,
	primary key (app_id, connection_id)
);

create table modelling.nwobject_nwgroup
(
    nwobject_id bigint,
    nwgroup_id bigint,
	primary key (nwobject_id, nwgroup_id)
);

create table modelling.nwgroup_connection
(
    nwgroup_id bigint,
    connection_id int,
	connection_field int, -- enum src=1, dest=2, ...
	primary key (nwgroup_id, connection_id, connection_field)
);

create table modelling.nwobject_connection -- (used only if settings flag is set)
(
    nwobject_id bigint,
    connection_id int,
	connection_field int, -- enum src=1, dest=2, ...
	primary key (nwobject_id, connection_id, connection_field)
);

create table modelling.service
(
 	id SERIAL PRIMARY KEY,
	app_id int,
	name Varchar,
	is_global boolean default false,
	port int,
	port_end int,
	proto_id int
);

create table modelling.service_group
(
	id SERIAL PRIMARY KEY,
	app_id int,
	name Varchar,
	is_global boolean default false,
	comment Varchar,
	creator Varchar,
	creation_date timestamp default now()
);

create table modelling.service_service_group
(
	service_id int,
    service_group_id int,
	primary key (service_id, service_group_id)
);

create table modelling.service_group_connection
(
    service_group_id int,
	connection_id int,
	primary key (service_group_id, connection_id)
);

create table modelling.service_connection -- (used only if settings flag is set)
(
    service_id int,
    connection_id int,
	primary key (service_id, connection_id)
);

create table modelling.change_history
(
	id BIGSERIAL PRIMARY KEY,
	app_id int,
	change_type int,
	object_type int,
    object_id bigint,
	change_text Varchar,
	changer Varchar,
	change_time Timestamp default now(),
	change_source Varchar default 'manual'
);
