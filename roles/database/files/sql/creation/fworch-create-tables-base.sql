-- the abs_hange_id is needed as it is incremented across 4 different tables
Create sequence  "public"."abs_change_id_seq"
Increment 1
Minvalue 1
Maxvalue 9223372036854775807
Cache 1;

-- the device_type table is only needed for the API
-- it allows for the pre-auth functions to work with hasura
Create table "device_type"
(
    "id"      int,
    "name"    VARCHAR
);

-- fundamental firewall data -------------------------------------

Create table "device" -- contains an entry for each firewall gateway
(
	"dev_id" SERIAL,
	"mgm_id" Integer NOT NULL,
	"dev_name" Varchar,
	"dev_uid" Varchar,
	"local_rulebase_name" Varchar,
	"local_rulebase_uid" Varchar,
	"global_rulebase_name" Varchar,
	"global_rulebase_uid" Varchar,
	"package_name" Varchar,
	"package_uid" Varchar,
	"dev_typ_id" Integer NOT NULL,
	"dev_active" Boolean NOT NULL Default true,
	"dev_comment" Text,
	"dev_create" Timestamp NOT NULL Default now(),
	"dev_update" Timestamp NOT NULL Default now(),
	"do_not_import" Boolean NOT NULL Default FALSE,
	"clearing_import_ran" Boolean NOT NULL Default FALSE,
	"force_initial_import" Boolean NOT NULL Default FALSE,
	"hide_in_gui" Boolean NOT NULL Default false,
 primary key ("dev_id")
);

Create table "management" -- contains an entry for each firewall management system
(
	"mgm_id" SERIAL,
	"dev_typ_id" Integer NOT NULL,
	"mgm_name" Varchar NOT NULL,
	"mgm_comment" Text,
 	"cloud_tenant_id" VARCHAR,
	"cloud_subscription_id" VARCHAR,	
	"mgm_create" Timestamp NOT NULL Default now(),
	"mgm_update" Timestamp NOT NULL Default now(),
	"import_credential_id" Integer NOT NULL,
	"ssh_hostname" Varchar NOT NULL,
	"ssh_port" Integer NOT NULL Default 22,
	"last_import_md5_complete_config" Varchar Default 0,
	"last_import_attempt" Timestamp,
	"last_import_attempt_successful" Boolean NOT NULL Default false,
	"do_not_import" Boolean NOT NULL Default FALSE,
	"clearing_import_ran" Boolean NOT NULL Default false,
	"force_initial_import" Boolean NOT NULL Default FALSE,
	"config_path" Varchar,
	"domain_uid" Varchar,
	"hide_in_gui" Boolean NOT NULL Default false,
	"importer_hostname" Varchar,
	"debug_level" Integer,
	"multi_device_manager_id" integer,		-- if this manager belongs to another multi_device_manager, then this id points to it
	"is_super_manager" BOOLEAN DEFAULT FALSE,
	"ext_mgm_data" Varchar,
	"mgm_uid" Varchar NOT NULL DEFAULT '',
	"rulebase_name" Varchar NOT NULL DEFAULT '',
	"rulebase_uid" Varchar NOT NULL DEFAULT '',
 primary key ("mgm_id")
);


create table import_credential
(
    id SERIAL PRIMARY KEY,
    credential_name varchar NOT NULL,
    is_key_pair BOOLEAN default FALSE,
    username varchar NOT NULL,
    secret text NOT NULL,
	public_key Text,
	cloud_client_id VARCHAR,
	cloud_client_secret VARCHAR
);

Create table "object"
(
	"obj_id" BIGSERIAL,
	"last_change_admin" Integer,
	"zone_id" Integer,
	"mgm_id" Integer NOT NULL,
	"obj_name" Varchar,
	"obj_comment" Varchar,
	"obj_uid" Text,
	"obj_typ_id" Integer NOT NULL,
	"obj_location" Varchar,
	"obj_member_names" Text,
	"obj_member_refs" Text,
	"initial_config" Boolean NOT NULL Default FALSE,
	"obj_sw" Varchar,
	"obj_ip" Cidr,
	"obj_ip_end" Cidr,
	"obj_nat" Boolean Default false,
	"nattyp_id" Integer,
	"obj_nat_ip" Cidr,
	"obj_nat_ip_end" Cidr,
	"obj_nat_install" Integer,
	"obj_color_id" Integer Default 1,
	"obj_sys_name" Varchar,
	"obj_sys_location" Varchar,
	"obj_sys_contact" Varchar,
	"obj_sys_desc" Text,
	"obj_sys_readcom" Varchar,
	"obj_sys_writecom" Varchar,
	"active" Boolean NOT NULL Default TRUE,
	"obj_create" BIGINT NOT NULL,
	"obj_last_seen" BIGINT NOT NULL,
	"removed" BIGINT,
 primary key ("obj_id")
);

Create table "objgrp"
(
	"objgrp_id" BIGINT NOT NULL,
	"objgrp_member_id" BIGINT NOT NULL,
	"import_created" BIGINT NOT NULL,
	"import_last_seen" BIGINT NOT NULL,
	"active" Boolean NOT NULL Default TRUE,
	"negated" Boolean NOT NULL Default FALSE,
	"removed" BIGINT,
 primary key ("objgrp_id","objgrp_member_id")
);

Create table "service"
(
	"svc_id" BIGSERIAL,
	"svc_uid" Text,
	"svc_name" Varchar,
	"svc_typ_id" Integer NOT NULL,
	"mgm_id" Integer,
	"svc_comment" Text,
	"svc_prod_specific" Text,
	"svc_member_names" Text,
	"svc_member_refs" Text,
	"svc_color_id" Integer Default 1,
	"ip_proto_id" Integer,
	"svc_port" Integer Check (svc_port is NULL OR (svc_port>=0 and svc_port<=65535)),
	"svc_port_end" Integer Check (svc_port_end IS NULL OR (svc_port_end>=0 and svc_port_end<=65535)),
	"initial_config" Boolean NOT NULL Default FALSE,
	"srv_keeponinstall" Boolean Default false,
	"svc_rpcnr" Varchar,
	"svc_code" Varchar,
	"svc_match" Text,
	"svc_source_port" Integer Check (svc_source_port is NULL OR (svc_source_port>=0 and svc_source_port<=65535)),
	"svc_source_port_end" Integer Check (svc_source_port_end is NULL OR (svc_source_port_end>=0 and svc_source_port_end<=65535)),
	"svc_tcp_res" Boolean Default false,
	"svc_accept_rep" Boolean Default false,
	"svc_accept_rep_any" Boolean Default false,
	"svc_mfa" Boolean Default false,
	"svc_timeout_std" Boolean Default false,
	"svc_timeout" Integer,
	"svc_sync" Boolean Default false,
	"svc_sync_delay" Boolean Default false,
	"svc_sync_delay_start" Integer,
	"active" Boolean NOT NULL Default TRUE,
	"last_change_admin" Integer,
	"svc_create" BIGINT NOT NULL,
	"svc_last_seen" BIGINT NOT NULL,
	"removed" BIGINT,
 primary key ("svc_id")
);

Create table "svcgrp"
(
	"svcgrp_id" BIGINT NOT NULL,
	"svcgrp_member_id" BIGINT NOT NULL,
	"import_created" BIGINT NOT NULL,
	"import_last_seen" BIGINT NOT NULL,
	"active" Boolean NOT NULL Default TRUE,
	"negated" Boolean NOT NULL Default FALSE,
	"removed" BIGINT,
 primary key ("svcgrp_id","svcgrp_member_id")
);

Create table "zone"
(
	"zone_id" SERIAL,
	"zone_create" BIGINT NOT NULL,
	"zone_last_seen" BIGINT NOT NULL,
	"mgm_id" Integer NOT NULL,
	"zone_name" Varchar NOT NULL,
	"active" Boolean NOT NULL Default TRUE,
	"removed" BIGINT,
 primary key ("zone_id")
);

Create table "usr"
(
	"user_id" BIGSERIAL PRIMARY KEY,
	"usr_typ_id" Integer NOT NULL,
	"user_color_id" Integer Default 1,
	"mgm_id" Integer NOT NULL,
	"user_name" Varchar NOT NULL,
	"active" Boolean NOT NULL Default TRUE,
	"user_member_names" Text,
	"user_member_refs" Text,
	"user_authmethod" Varchar,
	"user_valid_from" Date Default '1900-01-01',
	"user_valid_until" Date Default '9999-12-31',
	"src_restrict" Text,
	"dst_restrict" Text,
	"time_restrict" Text,
	"user_create" BIGINT NOT NULL,
	"user_last_seen" BIGINT NOT NULL,
	"user_comment" Text,
	"user_uid" Text,
	"user_firstname" Varchar,
	"user_lastname" Varchar,
	"last_change_admin" Integer,
	"tenant_id" Integer,
	"removed" BIGINT
);

Create table "usergrp"
(
	"usergrp_id" BIGINT,
	"usergrp_member_id" BIGINT,
	"import_created" BIGINT NOT NULL,
	"import_last_seen" BIGINT NOT NULL,
	"active" Boolean NOT NULL Default TRUE,
	"removed" BIGINT,
 primary key ("usergrp_id","usergrp_member_id")
);


-- uiuser - change metadata -------------------------------------

Create table "uiuser"
(
	"uiuser_id" SERIAL NOT NULL,
	"uiuser_username" Varchar,
	"uuid" Varchar NOT NULL UNIQUE,
	"uiuser_first_name" Varchar,
	"uiuser_last_name" Varchar,
	"uiuser_start_date" Date Default now(),
	"uiuser_end_date" Date,
	"uiuser_email" Varchar,
	"tenant_id" Integer,
	"uiuser_language" Varchar,
	"uiuser_password_must_be_changed" Boolean NOT NULL Default TRUE,
	"uiuser_last_login" Timestamp with time zone,
	"uiuser_last_password_change" Timestamp with time zone,
	"uiuser_pwd_history" Text,
	"ldap_connection_id" BIGINT,
 primary key ("uiuser_id")
);

-- text tables ----------------------------------------

Create table "language"
(
	"name" Varchar NOT NULL UNIQUE,
	"culture_info" Varchar NOT NULL,
	primary key ("name")
);

Create table "txt"
(
	"id" Varchar NOT NULL,
	"language" Varchar NOT NULL,
	"txt" Varchar NOT NULL,
 primary key ("id", "language")
);

Create table "customtxt"
(
	"id" Varchar NOT NULL,
	"language" Varchar NOT NULL,
	"txt" Varchar NOT NULL,
 	primary key ("id", "language")
);

Create table "error"
(
	"error_id" Varchar NOT NULL UNIQUE,
	"error_lvl" Integer NOT NULL,
	"error_txt_ger" Text NOT NULL,
	"error_txt_eng" Text NOT NULL,
 primary key ("error_id")
);

-- tenant -------------------------------------
Create table "tenant"
(
	"tenant_id" SERIAL,
	"tenant_name" Varchar NOT NULL UNIQUE,
	"tenant_projekt" Varchar,
	"tenant_comment" Text,
	"tenant_report" Boolean Default true,
	"tenant_can_view_all_devices" Boolean NOT NULL Default false,
	"tenant_is_superadmin" Boolean NOT NULL default false,	
	"tenant_create" Timestamp NOT NULL Default now(),
 primary key ("tenant_id")
);

Create table tenant_to_management
(
	tenant_id Integer NOT NULL,
	management_id Integer NOT NULL,
  	shared BOOLEAN NOT NULL DEFAULT TRUE,
  	primary key ("tenant_id", "management_id")
);

Create table "tenant_to_device"
(
	"tenant_id" Integer NOT NULL,
	"device_id" Integer NOT NULL,
	shared Boolean NOT NULL DEFAULT TRUE,
 primary key ("tenant_id", "device_id")
);

Create table "tenant_network"
(
	"tenant_net_id" BIGSERIAL,
	"tenant_id" Integer NOT NULL,
	"tenant_net_name" Varchar,
	"tenant_net_comment" Text,
	"tenant_net_ip" Cidr NOT NULL,
	"tenant_net_ip_end" Cidr NOT NULL,
	"tenant_net_create" Timestamp NOT NULL Default now(),
 primary key ("tenant_net_id")
);

-- basic static data -------------------------------------

Create table "import_control"
(
	"control_id" BIGSERIAL,
	"start_time" Timestamp NOT NULL Default now(),
	"stop_time" Timestamp,
	"import_type_id" INTEGER NOT NULL,
	"is_initial_import" Boolean NOT NULL Default FALSE,
	"mgm_id" Integer,
	"successful_import" Boolean NOT NULL Default FALSE,
	"policy_changes_found" Boolean NOT NULL Default FALSE, -- old_field: rule_changes_found
	"changes_found" Boolean NOT NULL Default FALSE, -- old_field: any_changes_found 
	"import_errors" Varchar,
	"notification_done" Boolean NOT NULL Default FALSE,
	"rule_owner_mapping_done" Boolean NOT NULL Default FALSE,
	"security_relevant_changes_counter" INTEGER NOT NULL Default 0,
 primary key ("control_id")
);

-- temporary table for storing the fw-relevant config during import
CREATE table "import_config" (
    "import_id" bigint NOT NULL,
    "mgm_id" integer NOT NULL,
    "config" jsonb NOT NULL,
	"start_import_flag" Boolean NOT NULL Default FALSE,
	"debug_mode" Boolean Default FALSE
);

-- todo: move this to git instead
-- permanent table for storing the full config as an archive
CREATE TABLE "import_full_config" (
    "import_id" bigint NOT NULL,
    "mgm_id" integer NOT NULL,
    "config" jsonb NOT NULL,
    PRIMARY KEY ("import_id")
);

CREATE TABLE "latest_config" (
    "mgm_id" integer NOT NULL,
    "import_id" bigint NOT NULL,
    "config" jsonb NOT NULL,
    PRIMARY KEY ("mgm_id")
);

---------------------------------------------------------------------------------------
-- adding interfaces and routing for path analysis
-- drop table if exists gw_route;
-- drop table if exists gw_interface;

create table gw_interface
(
    id SERIAL PRIMARY KEY,
    routing_device INTEGER NOT NULL,
    name VARCHAR NOT NULL,
    ip CIDR,
    state_up BOOLEAN DEFAULT TRUE,
    ip_version INTEGER NOT NULL DEFAULT 4,
    netmask_bits INTEGER NOT NULL
);

create table gw_route
(
    id SERIAL PRIMARY KEY,
    routing_device INT NOT NULL,
    target_gateway CIDR NOT NULL,
    destination CIDR NOT NULL,
    source CIDR,
    interface_id INT,
    interface VARCHAR,
    static BOOLEAN DEFAULT TRUE,
    metric INT,
    distance INT,
    ip_version INTEGER NOT NULL DEFAULT 4
);

-- (change)log tables -------------------------------------

Create table "log_data_issue"
(
	"data_issue_id" BIGSERIAL,
	"import_id" BIGINT,
	"object_name" Varchar,
	"object_uid" Varchar,
	"rule_uid" Varchar,				-- if a rule ref is broken
	"rule_id" BIGINT,				-- if a rule ref is broken
	"object_type" Varchar,
	"suspected_cause" VARCHAR,
	"description" VARCHAR,
	"issue_mgm_id" INTEGER,
	"issue_dev_id" INTEGER,
	"severity" INTEGER NOT NULL DEFAULT 1,
	"source" VARCHAR NOT NULL DEFAULT 'import',
	"issue_timestamp" TIMESTAMP DEFAULT NOW(),
	"user_id" INTEGER DEFAULT 0,
 primary key ("data_issue_id")
);

Create table "alert"
(
	"alert_id" BIGSERIAL,
	"ref_log_id" BIGINT,
	"ref_alert_id" BIGINT,
	"source" VARCHAR NOT NULL,
	"title" VARCHAR,
	"description" VARCHAR,
	"alert_mgm_id" INTEGER,
	"alert_dev_id" INTEGER,
	"alert_timestamp" TIMESTAMP DEFAULT NOW(),
	"user_id" INTEGER DEFAULT 0,
	"ack_by" INTEGER,
	"ack_timestamp" TIMESTAMP,
	"json_data" json,
	"alert_code" INTEGER,
 primary key ("alert_id")
);

create table notification
(
    id SERIAL PRIMARY KEY,
	notification_client Varchar,
	name Varchar,
	user_id int,
	owner_id int,
	channel Varchar,
	recipient_to Varchar,
    email_address_to Varchar,
	recipient_cc Varchar,
	email_address_cc Varchar,
	email_subject Varchar,
	layout Varchar,
	deadline Varchar,
	interval_before_deadline int,
	offset_before_deadline int,
	repeat_interval_after_deadline int,
	initial_offset_after_deadline int,
	repeat_offset_after_deadline int,
	repetitions_after_deadline int,
	last_sent Timestamp,
	email_body Varchar,
	schedule_id Integer,
	bundle_type Varchar,
	bundle_id Varchar
 );

-- configuration

Create table "ldap_connection"
(
	"ldap_connection_id" BIGSERIAL,
	"ldap_server" Varchar NOT NULL,
	"ldap_port" Integer NOT NULL Default 636,
	"ldap_tls" Boolean NOT NULL Default TRUE,
	"ldap_searchpath_for_users" Varchar NOT NULL,
	"ldap_searchpath_for_roles" Varchar,
	"ldap_tenant_level" Integer NOT NULL Default 1,
	"ldap_search_user" Varchar NOT NULL,
	"ldap_search_user_pwd" Varchar NOT NULL,
	"ldap_write_user" Varchar,
	"tenant_id" Integer,
	"ldap_write_user_pwd" Varchar,
	"ldap_searchpath_for_groups" Varchar,
	"ldap_writepath_for_groups" Varchar,
	"ldap_type" Integer NOT NULL Default 0,
	"ldap_pattern_length" Integer NOT NULL Default 0,
	"ldap_name" Varchar,
	"ldap_global_tenant_name" Varchar,
	"active" Boolean NOT NULL Default TRUE,
	primary key ("ldap_connection_id")
);

Create table "config"
(
	"config_key" VARCHAR NOT NULL,
	"config_value" VARCHAR,
	"config_user" Integer,
	primary key ("config_key","config_user")
);

create table time_object
(
    time_obj_id BIGSERIAL PRIMARY KEY,
    mgm_id Integer NOT NULL,
    time_obj_uid Varchar,
    time_obj_name Varchar,
    start_time TIMESTAMP WITH TIME ZONE,
    end_time TIMESTAMP WITH TIME ZONE,
    created BIGINT,
    removed BIGINT
);
