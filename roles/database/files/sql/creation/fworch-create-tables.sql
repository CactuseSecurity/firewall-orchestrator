/*
Created			29.04.2005
Last modified	14.07.2023
Project			Firewall Orchestrator
Contact			https://cactus.de/fworch
Database		PostgreSQL 9-13
*/

/* Create Sequence 

the abs_hange_id is needed as it is incremented across 4 different tables

*/

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
	"removed" BIGINT,
	"obj_create" BIGINT NOT NULL,
	"obj_last_seen" BIGINT NOT NULL,
 primary key ("obj_id")
);

Create table "objgrp"
(
	"objgrp_id" BIGINT NOT NULL,
	"objgrp_member_id" BIGINT NOT NULL,
	"import_created" BIGINT NOT NULL,
	"removed" BIGINT,
	"import_last_seen" BIGINT NOT NULL,
	"active" Boolean NOT NULL Default TRUE,
	"negated" Boolean NOT NULL Default FALSE,
 primary key ("objgrp_id","objgrp_member_id")
);

Create table "rule"
(
	"rule_id" BIGSERIAL,
	"last_change_admin" Integer,
	"rule_name" Varchar,
	"mgm_id" Integer NOT NULL,
	"parent_rule_id" BIGINT,
	"parent_rule_type" smallint,
	"active" Boolean NOT NULL Default TRUE,
	"removed" BIGINT,
	"rule_num" Integer NOT NULL,
	"rule_num_numeric" NUMERIC(16, 8),
	"rule_ruleid" Varchar,
	"rule_uid" Text,
	"rule_disabled" Boolean NOT NULL Default false,
	"rule_src_neg" Boolean NOT NULL Default false,
	"rule_dst_neg" Boolean NOT NULL Default false,
	"rule_svc_neg" Boolean NOT NULL Default false,
	"action_id" Integer NOT NULL,
	"track_id" Integer NOT NULL,
	"rule_src" Text NOT NULL,
	"rule_dst" Text NOT NULL,
	"rule_svc" Text NOT NULL,
	"rule_src_refs" Text,
	"rule_dst_refs" Text,
	"rule_svc_refs" Text,
	"rule_from_zone" Integer,
	"rule_to_zone" Integer,
	"rule_action" Text NOT NULL,
	"rule_track" Text NOT NULL,
	"rule_installon" Varchar,
	"rule_time" Varchar,
	"rule_comment" Text,
	"rule_head_text" Text,
	"rule_implied" Boolean NOT NULL Default FALSE,
	"rule_create" BIGINT NOT NULL,
	"rule_last_seen" BIGINT NOT NULL,
	"dev_id" Integer,
	"rule_custom_fields" jsonb,
	"access_rule" BOOLEAN Default TRUE,
	"nat_rule" BOOLEAN Default FALSE,
	"xlate_rule" BIGINT,
	"is_global" BOOLEAN DEFAULT FALSE NOT NULL,
	"rulebase_id" Integer NOT NULL,
	primary key ("rule_id")
);

-- rule_metadata contains rule related data that does not change when the rule itself is changed
Create table "rule_metadata"
(
	"rule_metadata_id" BIGSERIAL,
	"rule_uid" Text NOT NULL,
	"rule_created" Timestamp NOT NULL Default now(),
	"rule_last_modified" Timestamp NOT NULL Default now(),
	"rule_first_hit" Timestamp,
	"rule_last_hit" Timestamp,
	"rule_hit_counter" BIGINT,
	"rule_last_certified" Timestamp,
	"rule_last_certifier" Integer,
	"rule_last_certifier_dn" VARCHAR,
	"rule_owner" Integer, -- points to a uiuser (not an owner)
	"rule_owner_dn" Varchar, -- distinguished name pointing to ldap group, path or user
	"rule_to_be_removed" Boolean NOT NULL Default FALSE,
	"last_change_admin" Integer,
	"rule_decert_date" Timestamp,
	"rule_recertification_comment" Varchar,
 primary key ("rule_metadata_id")
);

-- adding direct link tables rule_[svc|nwobj|user]_resolved to make report object export easier
Create table "rule_svc_resolved"
(
	"mgm_id" INT,
	"rule_id" BIGINT NOT NULL,
	"svc_id" BIGINT NOT NULL,
	"created" BIGINT NOT NULL,
	"removed" BIGINT,
 primary key ("mgm_id","rule_id","svc_id")
);

Create table "rule_nwobj_resolved"
(
	"mgm_id" INT,
	"rule_id" BIGINT NOT NULL,
	"obj_id" BIGINT NOT NULL,
	"created" BIGINT NOT NULL,
	"removed" BIGINT,
 primary key ("mgm_id","rule_id","obj_id")
);

Create table "rule_user_resolved"
(
	"mgm_id" INT,
	"rule_id" BIGINT NOT NULL,
	"user_id" BIGINT NOT NULL,
	"created" BIGINT NOT NULL,
	"removed" BIGINT,
 primary key ("mgm_id","rule_id","user_id")
);

Create table "rule_from"
-- needs separate primary key as user_id can be null
(
	"rule_from_id" BIGSERIAL PRIMARY KEY,
	"rule_id" BIGINT NOT NULL,
	"obj_id" BIGINT NOT NULL,
	"user_id" BIGINT,
	"active" Boolean NOT NULL Default TRUE,
	"negated" Boolean NOT NULL Default FALSE,
	"removed" BIGINT,
	"rf_create" BIGINT NOT NULL,
	"rf_last_seen" BIGINT NOT NULL
);

Create table "rule_service"
(
	"rule_id" BIGINT NOT NULL,
	"svc_id" BIGINT NOT NULL,
	"active" Boolean NOT NULL Default TRUE,
	"rs_create" BIGINT NOT NULL,
	"rs_last_seen" BIGINT NOT NULL,
	"negated" Boolean NOT NULL Default FALSE,
	"removed" BIGINT,
 primary key ("rule_id","svc_id")
);

Create table "rule_to"
-- needs separate primary key as user_id can be null
(
	"rule_to_id" BIGSERIAL PRIMARY KEY,
	"rule_id" BIGINT NOT NULL,
	"obj_id" BIGINT NOT NULL,
	"user_id" BIGINT,
	"rt_create" BIGINT NOT NULL,
	"rt_last_seen" BIGINT NOT NULL,
	"active" Boolean NOT NULL Default TRUE,
	"removed" BIGINT,
	"negated" Boolean NOT NULL Default FALSE
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
	"removed" BIGINT,
	"svc_create" BIGINT NOT NULL,
	"svc_last_seen" BIGINT NOT NULL,
 primary key ("svc_id")
);

Create table "svcgrp"
(
	"svcgrp_id" BIGINT NOT NULL,
	"svcgrp_member_id" BIGINT NOT NULL,
	"import_created" BIGINT NOT NULL,
	"import_last_seen" BIGINT NOT NULL,
	"removed" BIGINT,
	"active" Boolean NOT NULL Default TRUE,
	"negated" Boolean NOT NULL Default FALSE,
 primary key ("svcgrp_id","svcgrp_member_id")
);

Create table "zone"
(
	"zone_id" SERIAL,
	"zone_create" BIGINT NOT NULL,
	"zone_last_seen" BIGINT NOT NULL,
	"removed" BIGINT,
	"mgm_id" Integer NOT NULL,
	"zone_name" Varchar NOT NULL,
	"active" Boolean NOT NULL Default TRUE,
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
	"removed" BIGINT,
	"user_comment" Text,
	"user_uid" Text,
	"user_firstname" Varchar,
	"user_lastname" Varchar,
	"last_change_admin" Integer,
	"tenant_id" Integer
);

Create table "usergrp"
(
	"usergrp_id" BIGINT,
	"usergrp_member_id" BIGINT,
	"import_created" BIGINT NOT NULL,
	"import_last_seen" BIGINT NOT NULL,
	"removed" BIGINT,
	"active" Boolean NOT NULL Default TRUE,
 primary key ("usergrp_id","usergrp_member_id")
);

Create table "usergrp_flat"
(
	"active" Boolean NOT NULL Default TRUE,
	"usergrp_flat_id" BIGINT NOT NULL,
	"usergrp_flat_member_id" BIGINT NOT NULL,
	"import_created" BIGINT NOT NULL,
	"import_last_seen" BIGINT NOT NULL,
	"removed" BIGINT,
 primary key ("usergrp_flat_id","usergrp_flat_member_id")
);

Create table "objgrp_flat"
(
	"objgrp_flat_id" BIGINT NOT NULL,
	"objgrp_flat_member_id" BIGINT NOT NULL,
	"active" Boolean NOT NULL Default TRUE,
	"import_created" BIGINT NOT NULL,
	"import_last_seen" BIGINT NOT NULL,
	"removed" BIGINT,
	"negated" Boolean NOT NULL Default FALSE
);

Create table "svcgrp_flat"
(
	"svcgrp_flat_id" BIGINT NOT NULL,
	"svcgrp_flat_member_id" BIGINT NOT NULL,
	"import_created" BIGINT NOT NULL,
	"import_last_seen" BIGINT NOT NULL,
	"active" Boolean NOT NULL Default TRUE,
	"removed" BIGINT,
	"negated" Boolean NOT NULL Default FALSE
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

Create table "parent_rule_type"
(
	"id" smallserial NOT NULL,
	"name" Varchar NOT NULL,
 primary key ("id")
);

Create table IF NOT EXISTS "stm_link_type"
(
	"id" SERIAL primary key,
	"name" Varchar NOT NULL
);

Create table "stm_action"
(
	"action_id" SERIAL,
	"action_name" Varchar NOT NULL,
	"allowed" BOOLEAN NOT NULL DEFAULT TRUE,
 primary key ("action_id")
);

Create table "stm_color"
(
	"color_id" SERIAL,
	"color_name" Varchar NOT NULL,
	"color_rgb" Char(7) NOT NULL,
	"color_comment" Text,
 primary key ("color_id")
);

Create table "stm_dev_typ"
(
	"dev_typ_id" SERIAL,
	"dev_typ_manufacturer" Varchar,
	"dev_typ_name" Varchar NOT NULL,
	"dev_typ_version" Varchar NOT NULL,
	"dev_typ_comment" Text,
	"dev_typ_predef_svc" Text,
	"dev_typ_predef_obj" Text,
	"dev_typ_is_mgmt" Boolean,
	"dev_typ_config_file_rules" Varchar,
	"dev_typ_config_file_basic_objects" Varchar,
	"dev_typ_config_file_users" Varchar,
	"dev_typ_is_multi_mgmt" Boolean Default FALSE,
	"is_pure_routing_device" Boolean Default FALSE,
 primary key ("dev_typ_id")
);

Create table "stm_obj_typ"
(
	"obj_typ_id" SERIAL,
	"obj_typ_name" Varchar NOT NULL,
	"obj_typ_comment" Text,
 primary key ("obj_typ_id")
);

Create table "stm_track"
(
	"track_id" SERIAL,
	"track_name" Varchar NOT NULL,
 primary key ("track_id")
);

Create table "stm_ip_proto"
(
	"ip_proto_id" Integer NOT NULL,
	"ip_proto_name" Varchar,
	"ip_proto_comment" Text,
 primary key ("ip_proto_id")
);

Create table "stm_svc_typ"
(
	"svc_typ_id" Integer NOT NULL UNIQUE,
	"svc_typ_name" Varchar,
	"svc_typ_comment" Text,
 primary key ("svc_typ_id")
);

Create table "stm_usr_typ"
(
	"usr_typ_id" Integer NOT NULL UNIQUE,
	"usr_typ_name" Varchar,
 primary key ("usr_typ_id")
);

-- only permanent import table -----------------------------------------------
-- these tables are only filled during an import run and the import data
-- is immediately removed afterwards

Create table "import_control"
(
	"control_id" BIGSERIAL,
	"start_time" Timestamp NOT NULL Default now(),
	"stop_time" Timestamp,
	"is_initial_import" Boolean NOT NULL Default FALSE,
	"delimiter_group" Varchar(3) NOT NULL Default '|',
	"delimiter_zone" Varchar(3) Default '%',
	"delimiter_user" Varchar(3) Default '@',
	"delimiter_list" Varchar(3) Default '|',
	"mgm_id" Integer NOT NULL,
	"last_change_in_config" Timestamp,
	"successful_import" Boolean NOT NULL Default FALSE,
	"changes_found" Boolean NOT NULL Default FALSE,
	"import_errors" Varchar,
	"notification_done" Boolean NOT NULL Default FALSE,
	"security_relevant_changes_counter" INTEGER NOT NULL Default 0,
	"is_full_import" BOOLEAN DEFAULT FALSE,
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

CREATE TABLE IF NOT EXISTS "latest_config" (
    "import_id" bigint NOT NULL,
    "mgm_id" integer NOT NULL,
    "config" jsonb NOT NULL,
    PRIMARY KEY ("import_id")
);

-- temporary import tables -------------------------------------

Create table "import_service"
(
	"svc_id" BIGSERIAL,
	"control_id" BIGINT NOT NULL,
	"svc_typ" Text NOT NULL,
	"svc_name" Varchar,
	"svc_comment" Text,
	"svc_color" Text Default 'black',
	"ip_proto" Text,
	"svc_prod_specific" Text,
	"rpc_nr" Varchar,
	"svc_uid" Text,
	"svc_port" Integer,
	"svc_port_end" Integer,
	"svc_source_port" Integer,
	"svc_source_port_end" Integer,
	"svc_timeout_std" Boolean Default false,
	"svc_timeout" Integer,
	"svc_member_names" Text,
	"svc_member_refs" Text,
	"last_change_admin" Varchar,
	"last_change_time" Timestamp,
	"svc_scope" Varchar,
 primary key ("svc_id","control_id")
);

Create table "import_object"
(
	"obj_id" BIGSERIAL,
	"obj_zone" Text,
	"obj_name" Varchar,
	"obj_typ" Text NOT NULL,
	"obj_member_names" Text,
	"obj_member_refs" Text,
	"obj_member_excludes" Text,
	"obj_sw" Varchar,
	"obj_ip" Cidr,
	"obj_ip_end" Cidr,
	"obj_color" Text Default 'black',
	"obj_comment" Text,
	"obj_location" Text,
	"control_id" BIGINT NOT NULL,
	"obj_uid" Text,
	"last_change_admin" Varchar,
	"last_change_time" Timestamp,
	"obj_scope" Varchar,
 primary key ("obj_id","control_id")
);

Create table "import_user"
(
	"user_id" BIGSERIAL,
	"control_id" BIGINT NOT NULL,
	"user_color" Text Default 'black',
	"user_name" Varchar NOT NULL,
	"user_typ" Text,
	"user_comment" Text,
	"user_authmethod" Varchar,
	"user_valid_from" Text,
	"user_valid_until" Text,
	"user_member_names" Text,
	"user_member_refs" Text,
	"user_uid" Text,
	"user_firstname" Text,
	"user_lastname" Text,
	"src_restrict" Text,
	"dst_restrict" Text,
	"time_restrict" Text,
	"last_change_admin" Varchar,
	"last_change_time" Timestamp,
	"user_scope" Varchar,
 primary key ("user_id","control_id")
);

Create table "import_rule"
(
	"control_id" BIGINT NOT NULL,
	"rule_id" BIGSERIAL,
	"rulebase_name" Varchar NOT NULL,
	"rule_num" Integer NOT NULL,
	"rule_uid" Text NOT NULL,
	"rule_ruleid" Varchar,
	"rule_name" Varchar,
	"rule_sysid" Varchar,
	"rule_disabled" Boolean Default false,
	"rule_src_neg" Boolean Default false,
	"rule_dst_neg" Boolean Default false,
	"rule_svc_neg" Boolean Default false,
	"rule_implied" Boolean Default FALSE,
	"rule_src" Text NOT NULL,
	"rule_dst" Text NOT NULL,
	"rule_from_zone" Text,
	"rule_to_zone" Text,
	"rule_svc" Text,
	"rule_action" Text NOT NULL,
	"rule_track" Text NOT NULL,
	"rule_installon" Varchar,
	"rule_time" Varchar,
	"rule_comment" Text,
	"rule_head_text" Text,
	"last_change_admin" Varchar,
	"last_change_time" Timestamp,
	"rule_scope" Varchar,
	"rule_src_refs" Text,
	"rule_dst_refs" Text,
	"rule_svc_refs" Text,
	"parent_rule_uid" Text,
	"rule_type" Varchar Default 'access',
	"last_hit" Timestamp,
	"rule_custom_fields" JSONB,
 primary key ("control_id","rule_id")
);

Create table "import_zone"
(
	"control_id" BIGINT NOT NULL,
	"zone_name" Text NOT NULL,
	"last_change_time" Timestamp
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

Create table "import_changelog"
(
	"change_time" Timestamp,
	"management_name" Varchar,
	"changed_object_name" Varchar,
	"changed_object_uid" Varchar,
	"changed_object_type" Varchar,
	"change_action" Varchar NOT NULL,
	"change_admin" Varchar,
	"control_id" BIGINT NOT NULL,
	"import_changelog_nr" BIGINT,
	"import_changelog_id" BIGSERIAL,
 primary key ("import_changelog_id")
);

Create table "changelog_object"
(
	"log_obj_id" BIGSERIAL,
	"new_obj_id" BIGINT Constraint "changelog_object_new_obj_id_constraint" Check ((change_action='D' AND new_obj_id IS NULL) OR NOT new_obj_id IS NULL),
	"old_obj_id" BIGINT Constraint "changelog_object_old_obj_id_constraint" Check ((change_action='I' AND old_obj_id IS NULL) OR NOT old_obj_id IS NULL),
	"import_admin" Integer,
	"doku_admin" Integer,
	"control_id" BIGINT NOT NULL,
	"abs_change_id" BIGINT NOT NULL Default nextval('public.abs_change_id_seq'::text) UNIQUE,
	"change_action" Char(1) NOT NULL,
	"changelog_obj_comment" Text,
	"documented" Boolean NOT NULL Default FALSE,
	"docu_time" Timestamp,
	"mgm_id" Integer NOT NULL,
	"change_type_id" Integer NOT NULL Default 3,
	"security_relevant" Boolean NOT NULL Default TRUE,
	"change_request_info" Varchar,
	"change_time" Timestamp,
	"unique_name" Varchar,
 primary key ("log_obj_id")
);

Create table "changelog_service"
(
	"log_svc_id" BIGSERIAL,
	"doku_admin" Integer,
	"control_id" BIGINT NOT NULL,
	"import_admin" Integer,
	"new_svc_id" BIGINT Constraint "changelog_service_new_svc_id_constraint" Check ((change_action='D' AND new_svc_id IS NULL) OR NOT new_svc_id IS NULL),
	"old_svc_id" BIGINT Constraint "changelog_service_old_svc_id_constraint" Check ((change_action='I' AND old_svc_id IS NULL) OR NOT old_svc_id IS NULL),
	"abs_change_id" BIGINT NOT NULL Default nextval('public.abs_change_id_seq'::text) UNIQUE,
	"change_action" Char(1) NOT NULL,
	"changelog_svc_comment" Text,
	"documented" Boolean NOT NULL Default FALSE,
	"docu_time" Timestamp,
	"mgm_id" Integer NOT NULL,
	"change_type_id" Integer NOT NULL Default 3,
	"security_relevant" Boolean NOT NULL Default TRUE,
	"change_request_info" Varchar,
	"change_time" Timestamp,
	"unique_name" Varchar,
 primary key ("log_svc_id")
);

Create table "changelog_user"
(
	"log_usr_id" BIGSERIAL,
	"new_user_id" BIGINT Constraint "changelog_user_new_user_id_constraint" Check ((change_action='D' AND new_user_id IS NULL) OR NOT new_user_id IS NULL),
	"old_user_id" BIGINT Constraint "changelog_user_old_user_id_contraint" Check ((change_action='I' AND old_user_id IS NULL) OR NOT old_user_id IS NULL),
	"import_admin" Integer,
	"doku_admin" Integer,
	"control_id" BIGINT NOT NULL,
	"abs_change_id" BIGINT NOT NULL Default nextval('public.abs_change_id_seq'::text) UNIQUE,
	"change_action" Char(1) NOT NULL,
	"changelog_user_comment" Text,
	"documented" Boolean NOT NULL Default FALSE,
	"docu_time" Timestamp,
	"mgm_id" Integer NOT NULL,
	"change_type_id" Integer NOT NULL Default 3,
	"security_relevant" Boolean NOT NULL Default TRUE,
	"change_request_info" Varchar,
	"change_time" Timestamp,
	"unique_name" Varchar,
 primary key ("log_usr_id")
);

Create table "changelog_rule"
(
	"log_rule_id" BIGSERIAL,
	"doku_admin" Integer,
	"control_id" BIGINT NOT NULL,
	"import_admin" Integer,
	"new_rule_id" BIGINT Constraint "changelog_rule_new_rule_id_constraint" Check ((change_action='D' AND new_rule_id IS NULL) OR NOT new_rule_id IS NULL),
	"old_rule_id" BIGINT Constraint "changelog_rule_old_rule_id_constraint" Check ((change_action='I' AND old_rule_id IS NULL) OR NOT old_rule_id IS NULL),
	"implicit_change" Boolean NOT NULL Default FALSE,
	"abs_change_id" BIGINT NOT NULL Default nextval('public.abs_change_id_seq'::text) UNIQUE,
	"change_action" Char(1) NOT NULL,
	"changelog_rule_comment" Text,
	"documented" Boolean NOT NULL Default FALSE,
	"docu_time" Timestamp,
	"mgm_id" Integer NOT NULL,
	"dev_id" Integer NOT NULL,
	"change_type_id" Integer NOT NULL Default 3,
	"security_relevant" Boolean NOT NULL Default TRUE,
	"change_request_info" Varchar,
	"change_time" Timestamp,
	"unique_name" Varchar,
 primary key ("log_rule_id")
);

Create table "stm_change_type"
(
	"change_type_id" SERIAL,
	"change_type_name" Varchar,
 primary key ("change_type_id")
);

-- reporting -------------------------------------------------------

Create table "report_template"
(
	"report_template_id" SERIAL,
	"report_filter" Varchar,
	"report_template_name" Varchar, --  NOT NULL Default "Report_"|"report_id"::VARCHAR,  -- user given name of a report
	"report_template_comment" TEXT,
	"report_template_create" Timestamp DEFAULT now(),
	"report_template_owner" Integer, --FK
	"filterline_history" Boolean Default TRUE, -- every time a filterline is sent, we save it for future usage (auto-deleted every 90 days)
	"report_parameters" json,
	primary key ("report_template_id")
);

Create table "report_format"
(
	"report_format_name" varchar not null,
 	primary key ("report_format_name")
);

Create table "report_schedule_format"
(
	"report_schedule_format_name" VARCHAR not null,
	"report_schedule_id" BIGSERIAL,
 	primary key ("report_schedule_format_name","report_schedule_id")
);

Create table "report"
(
	"report_id" BIGSERIAL,
	"report_template_id" Integer,
	"report_start_time" Timestamp,
	"report_end_time" Timestamp,
	"report_json" json NOT NULL,
	"report_pdf" text,
	"report_csv" text,
	"report_html" text,
	"report_name" varchar NOT NULL,
	"report_owner_id" Integer NOT NULL, --FK to uiuser
	"tenant_wide_visible" Integer,
	"report_type" Integer,
	"description" varchar,
 	primary key ("report_id")
);

Create table "report_schedule"
(
	"report_schedule_id" BIGSERIAL,
	"report_schedule_name" Varchar, --  NOT NULL Default "Report_"|"report_id"::VARCHAR,  -- user given name of a report
	"report_template_id" Integer, --FK
	"report_schedule_owner" Integer NOT NULL, --FK
	"report_schedule_start_time" Timestamp NOT NULL,  -- if day is bigger than 28, simply use the 1st of the next month, 00:00 am
	"report_schedule_repeat" Integer Not NULL Default 0, -- 0 do not repeat, 1 daily, 2 weekly, 3 monthly, 4 yearly 
	"report_schedule_every" Integer Not NULL Default 1, -- x - every x days/weeks/months/years
	"report_schedule_active" Boolean Default TRUE,
	"report_schedule_repetitions" Integer,
	"report_schedule_counter" Integer Not NULL Default 0,
 	primary key ("report_schedule_id")
);

Create table "report_template_viewable_by_user"
(
	"report_template_id" Integer NOT NULL,
	"uiuser_id" Integer NOT NULL,
 	primary key ("uiuser_id","report_template_id")
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

-- owner -------------------------------------------------------

create table owner
(
    id SERIAL PRIMARY KEY,
    name Varchar NOT NULL,
    dn Varchar NOT NULL,
    group_dn Varchar NOT NULL,
    is_default boolean default false,
    tenant_id int,
    recert_interval int,
    app_id_external varchar UNIQUE,
    last_recert_check Timestamp,
    recert_check_params Varchar,
	criticality Varchar,
	active boolean default true,
	import_source Varchar,
	common_service_possible boolean default false
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
    rule_metadata_id bigint
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
	next_recert_date Timestamp
);

Create Table IF NOT EXISTS "rule_enforced_on_gateway" 
(
	"rule_id" Integer NOT NULL,
	"dev_id" Integer,  --  NULL if rule is available for all gateways of its management
	"created" BIGINT,
	"removed" BIGINT
);

Create table IF NOT EXISTS "rulebase"
(
	"id" SERIAL primary key,
	"name" Varchar NOT NULL,
	"uid" Varchar NOT NULL,
	"mgm_id" Integer NOT NULL,
	"is_global" BOOLEAN DEFAULT FALSE NOT NULL,
	"created" BIGINT,
	"removed" BIGINT
);

Create table IF NOT EXISTS "rulebase_link"
(
	"id" SERIAL primary key,
	"gw_id" Integer,
	"from_rule_id" Integer,
	"to_rulebase_id" Integer NOT NULL,
	"link_type" Integer,
	"created" BIGINT,
	"removed" BIGINT
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
	locked boolean default false
);

-- workflow -------------------------------------------------------

-- create schema
create schema request;

CREATE TYPE rule_field_enum AS ENUM ('source', 'destination', 'service', 'rule');
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


--- Compliance ---
create schema compliance;

create table compliance.network_zone
(
    id BIGSERIAL PRIMARY KEY,
	name VARCHAR NOT NULL,
	description VARCHAR NOT NULL,
	super_network_zone_id bigint,
	owner_id bigint
);

create table compliance.network_zone_communication
(
    from_network_zone_id bigint NOT NULL,
	to_network_zone_id bigint NOT NULL
);

create table compliance.ip_range
(
    network_zone_id bigint NOT NULL,
	ip_range_start inet NOT NULL,
	ip_range_end inet NOT NULL,
	PRIMARY KEY(network_zone_id, ip_range_start, ip_range_end)
);


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
	extra_params Varchar
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
	change_time Timestamp default now()
);
