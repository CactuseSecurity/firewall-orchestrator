/*
Created		29.04.2005
Modified	21.07.2020
Project		IT Security Organizer
Company		Cactus eSecurity GmbH
Database	PostgreSQL 9-12
*/

/* Create Sequences */

Create sequence "public"."abs_change_id_seq"
Increment 1
Minvalue 1
Maxvalue 9223372036854775807
Cache 1;

/* Create Tables */

Create table "device"
(
	"dev_id" SERIAL,
	"mgm_id" Integer NOT NULL,
	"dev_name" Varchar,
	"dev_rulebase" Varchar,
	"dev_typ_id" Integer NOT NULL,
	"client_id" Integer,
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

Create table "client_project"
(
	"client_id" Integer NOT NULL,
	"prj_id" Integer NOT NULL,
 primary key ("client_id","prj_id")
);

Create table "client"
(
	"client_id" SERIAL,
	"client_name" Varchar NOT NULL,
	"client_projekt" Varchar,
	"client_comment" Text,
	"client_report" Boolean Default true,
	"client_create" Timestamp NOT NULL Default now(),
 primary key ("client_id")
);

Create table "client_object"
(
	"client_id" Integer NOT NULL,
	"obj_id" Integer NOT NULL,
 primary key ("client_id","obj_id")
);

Create table "client_network"
(
	"client_net_id" BIGSERIAL,
	"client_id" Integer NOT NULL,
	"client_net_name" Varchar,
	"client_net_comment" Text,
	"client_net_ip" Cidr,
	"client_net_ip_end" Cidr,
	"client_net_create" Timestamp NOT NULL Default now(),
 primary key ("client_net_id")
);

Create table "management"
(
	"mgm_id" SERIAL,
	"dev_typ_id" Integer NOT NULL,
	"mgm_name" Varchar NOT NULL,
	"mgm_comment" Text,
	"client_id" Integer,
	"mgm_create" Timestamp NOT NULL Default now(),
	"mgm_update" Timestamp NOT NULL Default now(),
	"ssh_public_key" Text NOT NULL Default 'leer',
	"ssh_private_key" Text NOT NULL,
	"ssh_hostname" Varchar NOT NULL,
	"ssh_port" Integer NOT NULL Default 22,
	"ssh_user" Varchar NOT NULL Default 'itsecorg',
	"last_import_md5_complete_config" Varchar Default 0,
	"last_import_md5_rules" Varchar Default 0,
	"last_import_md5_objects" Varchar Default 0,
	"last_import_md5_users" Varchar Default 0,
	"do_not_import" Boolean NOT NULL Default FALSE,
	"clearing_import_ran" Boolean NOT NULL Default false,
	"force_initial_import" Boolean NOT NULL Default FALSE,
	"config_path" Varchar,
	"hide_in_gui" Boolean NOT NULL Default false,
	"importer_hostname" Varchar,
 primary key ("mgm_id")
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
	"obj_create" Integer NOT NULL,
	"obj_last_seen" Integer NOT NULL,
 primary key ("obj_id")
);

Create table "objgrp"
(
	"objgrp_id" Integer NOT NULL,
	"objgrp_member_id" Integer NOT NULL,
	"import_created" Integer NOT NULL,
	"import_last_seen" Integer NOT NULL,
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
	"active" Boolean NOT NULL Default TRUE,
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
	"rule_create" Integer NOT NULL,
	"rule_last_seen" Integer NOT NULL,
	"dev_id" Integer,
 primary key ("rule_id")
);

Create table "rule_from"
(
	"rule_from_id" BIGSERIAL,
	"rf_create" Integer NOT NULL,
	"rf_last_seen" Integer NOT NULL,
	"rule_id" Integer NOT NULL,
	"obj_id" Integer NOT NULL,
	"user_id" Integer,
	"active" Boolean NOT NULL Default TRUE,
	"negated" Boolean NOT NULL Default FALSE,
 primary key ("rule_from_id")
);

Create table "rule_review"
(
	"rule_id" Integer NOT NULL,
	"client_id" Integer NOT NULL,
	"rr_comment" Text,
	"rr_visible" Boolean NOT NULL Default true,
	"rr_create" Timestamp NOT NULL Default now(),
	"rr_update" Timestamp NOT NULL Default now(),
 primary key ("rule_id","client_id")
);

Create table "rule_service"
(
	"rule_id" Integer NOT NULL,
	"svc_id" Integer NOT NULL,
	"active" Boolean NOT NULL Default TRUE,
	"rs_create" Integer NOT NULL,
	"rs_last_seen" Integer NOT NULL,
	"negated" Boolean NOT NULL Default FALSE,
 primary key ("rule_id","svc_id")
);

Create table "rule_to"
(
	"rule_id" Integer NOT NULL,
	"obj_id" Integer NOT NULL,
	"rt_create" Integer NOT NULL,
	"rt_last_seen" Integer NOT NULL,
	"active" Boolean NOT NULL Default TRUE,
	"negated" Boolean NOT NULL Default FALSE,
 primary key ("rule_id","obj_id")
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
	"svc_create" Integer NOT NULL,
	"svc_last_seen" Integer NOT NULL,
 primary key ("svc_id")
);

Create table "svcgrp"
(
	"svcgrp_id" Integer NOT NULL,
	"svcgrp_member_id" Integer NOT NULL,
	"import_created" Integer NOT NULL,
	"import_last_seen" Integer NOT NULL,
	"active" Boolean NOT NULL Default TRUE,
	"negated" Boolean NOT NULL Default FALSE,
 primary key ("svcgrp_id","svcgrp_member_id")
);

Create table "stm_action"
(
	"action_id" SERIAL,
	"action_name" Varchar NOT NULL,
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
 primary key ("dev_typ_id")
);

Create table "stm_nattyp"
(
	"nattyp_id" SERIAL,
	"nattyp_name" Varchar NOT NULL,
	"nattyp_comment" Text,
 primary key ("nattyp_id")
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

Create table "zone"
(
	"zone_id" BIGSERIAL,
	"zone_create" Integer NOT NULL,
	"zone_last_seen" Integer NOT NULL,
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
	"user_create" Integer NOT NULL,
	"user_last_seen" Integer NOT NULL,
	"user_comment" Text,
	"user_uid" Text,
	"user_firstname" Varchar,
	"user_lastname" Varchar,
	"last_change_admin" Integer,
	"client_id" Integer
);

Create table "usergrp"
(
	"usergrp_id" BIGSERIAL,
	"usergrp_member_id" BIGSERIAL,
	"import_created" Integer NOT NULL,
	"import_last_seen" Integer NOT NULL,
	"active" Boolean NOT NULL Default TRUE,
 primary key ("usergrp_id","usergrp_member_id")
);

Create table "import_service"
(
	"svc_id" BIGSERIAL,
	"control_id" Integer NOT NULL,
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
	"control_id" Integer NOT NULL,
	"obj_uid" Text,
	"last_change_admin" Varchar,
	"last_change_time" Timestamp,
	"obj_scope" Varchar,
 primary key ("obj_id","control_id")
);

Create table "import_user"
(
	"user_id" BIGSERIAL,
	"control_id" Integer NOT NULL,
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
	"control_id" Integer NOT NULL,
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
 primary key ("control_id","rule_id")
);

Create table "import_control"
(
	"control_id" BIGSERIAL,
	"start_time" Timestamp NOT NULL Default now(),
	"stop_time" Timestamp,
	"config_name" Text,
	"mgm_product" Text,
	"mgm_version" Text,
	"is_initial_import" Boolean NOT NULL Default FALSE,
	"delimiter_group" Varchar(3) NOT NULL Default '|',
	"delimiter_zone" Varchar(3) Default '%',
	"delimiter_user" Varchar(3) Default '@',
	"delimiter_list" Varchar(3) Default '|',
	"mgm_id" Integer NOT NULL,
	"last_change_in_config" Timestamp,
	"successful_import" Boolean NOT NULL Default FALSE,
	"import_errors" Varchar,
 primary key ("control_id")
);

Create table "import_zone"
(
	"control_id" Integer NOT NULL,
	"zone_name" Text NOT NULL,
	"last_change_time" Timestamp
);

Create table "changelog_object"
(
	"log_obj_id" BIGSERIAL,
	"new_obj_id" Integer Constraint "changelog_object_new_obj_id_constraint" Check ((change_action='D' AND new_obj_id IS NULL) OR NOT new_obj_id IS NULL),
	"old_obj_id" Integer Constraint "changelog_object_old_obj_id_constraint" Check ((change_action='I' AND old_obj_id IS NULL) OR NOT old_obj_id IS NULL),
	"import_admin" Integer,
	"doku_admin" Integer,
	"control_id" Integer NOT NULL,
	"abs_change_id" Integer NOT NULL Default nextval('public.abs_change_id_seq'::text) UNIQUE,
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

Create table "isoadmin"
(
	"isoadmin_id" Integer NOT NULL,
	"isoadmin_username" Varchar NOT NULL UNIQUE,
	"isoadmin_first_name" Varchar,
	"isoadmin_last_name" Varchar,
	"isoadmin_password" Varchar,
	"isoadmin_start_date" Date Default now(),
	"isoadmin_end_date" Date,
	"isoadmin_email" Varchar,
	"client_id" Integer,
	"isoadmin_password_must_be_changed" Boolean NOT NULL Default TRUE,
	"isoadmin_last_login" Timestamp with time zone,
	"isoadmin_last_password_change" Timestamp with time zone,
	"isoadmin_pwd_history" Text,
 primary key ("isoadmin_id")
);

Create table "error"
(
	"error_id" Varchar NOT NULL UNIQUE,
	"error_lvl" Integer NOT NULL,
	"error_txt_ger" Text NOT NULL,
	"error_txt_eng" Text NOT NULL,
 primary key ("error_id")
);

Create table "changelog_service"
(
	"log_svc_id" BIGSERIAL,
	"doku_admin" Integer,
	"control_id" Integer NOT NULL,
	"import_admin" Integer,
	"new_svc_id" Integer Constraint "changelog_service_new_svc_id_constraint" Check ((change_action='D' AND new_svc_id IS NULL) OR NOT new_svc_id IS NULL),
	"old_svc_id" Integer Constraint "changelog_service_old_svc_id_constraint" Check ((change_action='I' AND old_svc_id IS NULL) OR NOT old_svc_id IS NULL),
	"abs_change_id" Integer NOT NULL Default nextval('public.abs_change_id_seq'::text) UNIQUE,
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
	"new_user_id" Integer Constraint "changelog_user_new_user_id_constraint" Check ((change_action='D' AND new_user_id IS NULL) OR NOT new_user_id IS NULL),
	"old_user_id" Integer Constraint "changelog_user_old_user_id_contraint" Check ((change_action='I' AND old_user_id IS NULL) OR NOT old_user_id IS NULL),
	"import_admin" Integer,
	"doku_admin" Integer,
	"control_id" Integer NOT NULL,
	"abs_change_id" Integer NOT NULL Default nextval('public.abs_change_id_seq'::text) UNIQUE,
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
	"control_id" Integer NOT NULL,
	"import_admin" Integer,
	"new_rule_id" Integer Constraint "changelog_rule_new_rule_id_constraint" Check ((change_action='D' AND new_rule_id IS NULL) OR NOT new_rule_id IS NULL),
	"old_rule_id" Integer Constraint "changelog_rule_old_rule_id_constraint" Check ((change_action='I' AND old_rule_id IS NULL) OR NOT old_rule_id IS NULL),
	"implicit_change" Boolean NOT NULL Default FALSE,
	"abs_change_id" Integer NOT NULL Default nextval('public.abs_change_id_seq'::text) UNIQUE,
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

Create table "error_log"
(
	"error_log_id" BIGSERIAL,
	"error_id" Varchar NOT NULL,
	"error_txt" Text,
	"error_time" Timestamp NOT NULL Default now(),
 primary key ("error_log_id")
);

Create table "config"
(
	"config_id" BIGSERIAL,
	"language" VARCHAR Default 'english',
 primary key ("config_id")
);

Create table "stm_usr_typ"
(
	"usr_typ_id" Integer NOT NULL UNIQUE,
	"usr_typ_name" Varchar,
 primary key ("usr_typ_id")
);

Create table "text_msg"
(
	"text_msg_id" Varchar NOT NULL UNIQUE,
	"text_msg_ger" Text NOT NULL,
	"text_msg_eng" Text NOT NULL,
 primary key ("text_msg_id")
);

Create table "rule_order"
(
	"control_id" Integer NOT NULL,
	"dev_id" Integer NOT NULL,
	"rule_id" Integer NOT NULL,
	"rule_number" Integer NOT NULL,
 primary key ("control_id","dev_id","rule_id")
);

Create table "objgrp_flat"
(
	"objgrp_flat_id" Integer NOT NULL,
	"objgrp_flat_member_id" Integer NOT NULL,
	"active" Boolean NOT NULL Default TRUE,
	"import_created" Integer NOT NULL,
	"import_last_seen" Integer NOT NULL,
	"negated" Boolean NOT NULL Default FALSE
);

Create table "svcgrp_flat"
(
	"svcgrp_flat_id" Integer NOT NULL,
	"svcgrp_flat_member_id" Integer NOT NULL,
	"import_created" Integer NOT NULL,
	"import_last_seen" Integer NOT NULL,
	"active" Boolean NOT NULL Default TRUE,
	"negated" Boolean NOT NULL Default FALSE
);

Create table "stm_report_typ"
(
	"report_typ_id" SERIAL,
	"report_typ_name_german" Varchar NOT NULL,
	"report_typ_name_english" Varchar,
	"report_typ_comment_german" Text,
	"report_typ_comment_english" Text,
 primary key ("report_typ_id")
);

Create table "client_user"
(
	"user_id" BIGSERIAL,
	"client_id" BIGSERIAL,
 primary key ("user_id","client_id")
);

Create table "request"
(
	"request_id" BIGSERIAL,
	"request_number" Varchar,
	"request_time" Timestamp,
	"request_received" Timestamp,
	"request_submitter" Varchar,
	"request_approver" Varchar,
	"client_id" Integer,
	"request_type_id" Integer,
 primary key ("request_id")
);

Create table "request_object_change"
(
	"log_obj_id" Integer NOT NULL,
	"request_id" Integer NOT NULL,
 primary key ("log_obj_id","request_id")
);

Create table "request_service_change"
(
	"log_svc_id" Integer NOT NULL,
	"request_id" Integer NOT NULL,
 primary key ("log_svc_id","request_id")
);

Create table "request_rule_change"
(
	"log_rule_id" Integer NOT NULL,
	"request_id" Integer NOT NULL,
 primary key ("log_rule_id","request_id")
);

Create table "request_user_change"
(
	"log_usr_id" Integer NOT NULL,
	"request_id" Integer NOT NULL,
 primary key ("log_usr_id","request_id")
);

Create table "usergrp_flat"
(
	"active" Boolean NOT NULL Default TRUE,
	"usergrp_flat_id" Integer NOT NULL,
	"usergrp_flat_member_id" Integer NOT NULL,
	"import_created" Integer NOT NULL,
	"import_last_seen" Integer NOT NULL,
 primary key ("usergrp_flat_id","usergrp_flat_member_id")
);

Create table "stm_change_type"
(
	"change_type_id" SERIAL,
	"change_type_name" Varchar,
 primary key ("change_type_id")
);

Create table "manual"
(
	"id" Varchar(6) NOT NULL UNIQUE,
	"topic_l1_id" Varchar,
	"topic_l2_id" Varchar,
	"topic_l1_txt_eng" Varchar,
	"topic_l1_txt_ger" Varchar,
	"topic_l2_txt_eng" Varchar,
	"topic_l2_txt_ger" Varchar,
	"body_txt_eng" Text,
	"body_txt_ger" Text,
 primary key ("id")
);

Create table "temp_table_for_client_filtered_rule_ids"
(
	"rule_id" Integer NOT NULL,
	"report_id" Integer NOT NULL,
 primary key ("rule_id","report_id")
);

Create table "client_username"
(
	"client_username_id" BIGSERIAL,
	"client_id" Integer,
	"client_username_pattern" Varchar,
	"client_username_comment" Text,
	"client_username_create" Timestamp NOT NULL Default now(),
 primary key ("client_username_id")
);

Create table "request_type"
(
	"request_type_id" Integer NOT NULL UNIQUE,
	"request_type_name" Varchar NOT NULL UNIQUE,
	"request_type_comment" Varchar,
 primary key ("request_type_id")
);

Create table "temp_filtered_rule_ids"
(
	"report_id" Integer NOT NULL,
	"rule_id" Integer NOT NULL
);

Create table "temp_mgmid_importid_at_report_time"
(
	"control_id" Integer,
	"mgm_id" Integer,
	"report_id" Integer NOT NULL
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
	"control_id" Integer NOT NULL,
	"import_changelog_nr" Integer,
	"import_changelog_id" BIGSERIAL,
 primary key ("import_changelog_id")
);

Create table "device_client_map"
(
	"client_id" Integer NOT NULL,
	"dev_id" Integer NOT NULL,
 primary key ("client_id","dev_id")
);

Create table "management_client_map"
(
	"client_id" Integer NOT NULL,
	"mgm_id" Integer NOT NULL,
 primary key ("client_id","mgm_id")
);

Create table "reporttyp_client_map"
(
	"client_id" Integer NOT NULL,
	"report_typ_id" Integer NOT NULL,
 primary key ("client_id","report_typ_id")
);

Create table "report"
(
	"report_id" BIGSERIAL,
	"report_typ_id" Integer NOT NULL,
	"start_import_id" Integer NOT NULL,
	"stop_import_id" Integer,
	"dev_id" Integer NOT NULL,
	"report_generation_time" Timestamp NOT NULL Default now(),
	"report_start_time" Timestamp,
	"report_end_time" Timestamp,
	"report_document" Text NOT NULL,
	"client_id" Integer,
 primary key ("report_id")
);

Create table "role"
(
	"role_id" SERIAL,
	"role_name" Varchar NOT NULL,
	"role_can_view_all_devices" Boolean NOT NULL Default false,
	"role_is_superadmin" Boolean NOT NULL default false,	
 primary key ("role_id")
);

Create table "role_to_user"
(
	"role_id" Integer NOT NULL,
	"user_id" Integer NOT NULL,
 primary key ("role_id", "user_id")
);

Create table "role_to_device"
(
	"role_id" Integer NOT NULL,
	"device_id" Integer NOT NULL,
 primary key ("role_id", "device_id")
);

/* Create Tab 'Others' for Selected Tables */


/* Create Alternate Keys */
Alter Table "object" add Constraint "obj_altkey" UNIQUE ("mgm_id","zone_id","obj_uid","obj_create");
Alter Table "rule" add Constraint "rule_altkey" UNIQUE ("mgm_id","rule_uid","rule_create");
Alter Table "service" add Constraint "svc_altkey" UNIQUE ("mgm_id","svc_uid","svc_create");
Alter Table "stm_dev_typ" add Constraint "Alter_Key1" UNIQUE ("dev_typ_name","dev_typ_version");
Alter Table "zone" add Constraint "Alter_Key10" UNIQUE ("mgm_id","zone_name");
Alter Table "usr" add Constraint "usr_altkey" UNIQUE ("mgm_id","user_name","user_create");
Alter Table "changelog_object" add Constraint "alt_key_changelog_object" UNIQUE ("abs_change_id");
Alter Table "changelog_service" add Constraint "alt_key_changelog_service" UNIQUE ("abs_change_id");
Alter Table "changelog_user" add Constraint "alt_key_changelog_user" UNIQUE ("abs_change_id");
Alter Table "changelog_rule" add Constraint "alt_key_changelog_rule" UNIQUE ("abs_change_id");
Alter Table "temp_filtered_rule_ids" add Constraint "temp_filtered_rule_ids_alt_key" UNIQUE ("report_id","rule_id");
Alter Table "temp_mgmid_importid_at_report_time" add Constraint "Alter_Key13" UNIQUE ("control_id","mgm_id","report_id");
Alter Table "import_changelog" add Constraint "Alter_Key14" UNIQUE ("import_changelog_nr","control_id");



/* Create Indexes */
Create unique index "firewall_akey" on "device" using btree ("mgm_id","dev_id");
Create index "kunden_akey" on "client" using btree ("client_name");
Create unique index "kundennetze_akey" on "client_network" using btree ("client_net_id","client_id");
Create unique index "management_akey" on "management" using btree ("mgm_name","client_id");
Create index "rule_index" on "rule" using btree ("mgm_id","rule_id","rule_uid","dev_id");
Create unique index "rule_from_unique_index" on "rule_from" using btree ("rule_id","obj_id","user_id");
Create unique index "stm_color_akey" on "stm_color" using btree ("color_name");
Create index "stm_fw_typ_a2key" on "stm_dev_typ" using btree ("dev_typ_name");
Create unique index "stm_fw_typ_akey" on "stm_dev_typ" using btree ("dev_typ_name","dev_typ_version");
Create index "stm_nattypes_akey" on "stm_nattyp" using btree ("nattyp_name");
Create unique index "stm_obj_typ_akey" on "stm_obj_typ" using btree ("obj_typ_name");
Create index "import_control_start_time_idx" on "import_control" using btree ("start_time");
Create index "rule_oder_idx" on "rule_order" using btree ("control_id","rule_id");



/* Create Foreign Keys */
Create index "IX_relationship11" on "object" ("obj_nat_install");
Alter table "object" add  foreign key ("obj_nat_install") references "device" ("dev_id") on update restrict on delete restrict;
Create index "IX_Relationship126" on "rule_order" ("dev_id");
Alter table "rule_order" add  foreign key ("dev_id") references "device" ("dev_id") on update restrict on delete restrict;
Create index "IX_Relationship128" on "changelog_rule" ("dev_id");
Alter table "changelog_rule" add  foreign key ("dev_id") references "device" ("dev_id") on update restrict on delete restrict;
Create index "IX_Relationship186" on "rule" ("dev_id");
Alter table "rule" add  foreign key ("dev_id") references "device" ("dev_id") on update restrict on delete restrict;
Create index "IX_Relationship192" on "device_client_map" ("dev_id");
Alter table "device_client_map" add  foreign key ("dev_id") references "device" ("dev_id") on update restrict on delete restrict;
Create index "IX_Relationship205" on "report" ("dev_id");
Alter table "report" add  foreign key ("dev_id") references "device" ("dev_id") on update restrict on delete restrict;
Create index "IX_relationship7" on "device" ("client_id");
Alter table "device" add  foreign key ("client_id") references "client" ("client_id") on update restrict on delete restrict;
Create index "IX_relationship1" on "client_project" ("client_id");
Alter table "client_project" add  foreign key ("client_id") references "client" ("client_id") on update restrict on delete restrict;
Create index "IX_relationship2" on "client_project" ("prj_id");
Alter table "client_project" add  foreign key ("prj_id") references "client" ("client_id") on update restrict on delete restrict;
Create index "IX_relationship15" on "client_object" ("client_id");
Alter table "client_object" add  foreign key ("client_id") references "client" ("client_id") on update restrict on delete restrict;
Create index "IX_relationship3" on "client_network" ("client_id");
Alter table "client_network" add  foreign key ("client_id") references "client" ("client_id") on update restrict on delete restrict;
Create index "IX_relationship4" on "management" ("client_id");
Alter table "management" add  foreign key ("client_id") references "client" ("client_id") on update restrict on delete restrict;
Create index "IX_relationship32" on "rule_review" ("client_id");
Alter table "rule_review" add  foreign key ("client_id") references "client" ("client_id") on update restrict on delete restrict;
Create index "IX_Relationship135" on "client_user" ("client_id");
Alter table "client_user" add  foreign key ("client_id") references "client" ("client_id") on update restrict on delete restrict;
Create index "IX_Relationship148" on "request" ("client_id");
Alter table "request" add  foreign key ("client_id") references "client" ("client_id") on update restrict on delete restrict;
Create index "IX_Relationship165" on "usr" ("client_id");
Alter table "usr" add  foreign key ("client_id") references "client" ("client_id") on update restrict on delete restrict;
Create index "IX_Relationship180" on "client_username" ("client_id");
Alter table "client_username" add  foreign key ("client_id") references "client" ("client_id") on update restrict on delete restrict;
Create index "IX_Relationship188" on "isoadmin" ("client_id");
Alter table "isoadmin" add  foreign key ("client_id") references "client" ("client_id") on update restrict on delete restrict;
Create index "IX_Relationship189" on "device_client_map" ("client_id");
Alter table "device_client_map" add  foreign key ("client_id") references "client" ("client_id") on update restrict on delete restrict;
Create index "IX_Relationship190" on "management_client_map" ("client_id");
Alter table "management_client_map" add  foreign key ("client_id") references "client" ("client_id") on update restrict on delete restrict;
Create index "IX_Relationship193" on "reporttyp_client_map" ("client_id");
Alter table "reporttyp_client_map" add  foreign key ("client_id") references "client" ("client_id") on update restrict on delete restrict;
Create index "IX_Relationship206" on "report" ("client_id");
Alter table "report" add  foreign key ("client_id") references "client" ("client_id") on update restrict on delete restrict;
Create index "IX_relationship5" on "device" ("mgm_id");
Alter table "device" add  foreign key ("mgm_id") references "management" ("mgm_id") on update restrict on delete restrict;
Create index "IX_relationship8" on "object" ("mgm_id");
Alter table "object" add  foreign key ("mgm_id") references "management" ("mgm_id") on update restrict on delete restrict;
Create index "IX_relationship21" on "rule" ("mgm_id");
Alter table "rule" add  foreign key ("mgm_id") references "management" ("mgm_id") on update restrict on delete restrict;
Create index "IX_relationship17" on "service" ("mgm_id");
Alter table "service" add  foreign key ("mgm_id") references "management" ("mgm_id") on update restrict on delete restrict;
Create index "IX_Relationship38" on "zone" ("mgm_id");
Alter table "zone" add  foreign key ("mgm_id") references "management" ("mgm_id") on update restrict on delete restrict;
Create index "IX_Relationship43" on "usr" ("mgm_id");
Alter table "usr" add  foreign key ("mgm_id") references "management" ("mgm_id") on update restrict on delete restrict;
Create index "IX_Relationship127" on "changelog_rule" ("mgm_id");
Alter table "changelog_rule" add  foreign key ("mgm_id") references "management" ("mgm_id") on update restrict on delete restrict;
Create index "IX_Relationship129" on "changelog_user" ("mgm_id");
Alter table "changelog_user" add  foreign key ("mgm_id") references "management" ("mgm_id") on update restrict on delete restrict;
Create index "IX_Relationship130" on "changelog_object" ("mgm_id");
Alter table "changelog_object" add  foreign key ("mgm_id") references "management" ("mgm_id") on update restrict on delete restrict;
Create index "IX_Relationship131" on "changelog_service" ("mgm_id");
Alter table "changelog_service" add  foreign key ("mgm_id") references "management" ("mgm_id") on update restrict on delete restrict;
Create index "IX_Relationship184" on "temp_mgmid_importid_at_report_time" ("mgm_id");
Alter table "temp_mgmid_importid_at_report_time" add  foreign key ("mgm_id") references "management" ("mgm_id") on update restrict on delete restrict;
Create index "IX_Relationship191" on "management_client_map" ("mgm_id");
Alter table "management_client_map" add  foreign key ("mgm_id") references "management" ("mgm_id") on update restrict on delete restrict;
Create index "IX_relationship16" on "client_object" ("obj_id");
Alter table "client_object" add  foreign key ("obj_id") references "object" ("obj_id") on update restrict on delete restrict;
Create index "IX_relationship13" on "objgrp" ("objgrp_id");
Alter table "objgrp" add  foreign key ("objgrp_id") references "object" ("obj_id") on update restrict on delete restrict;
Create index "IX_relationship14" on "objgrp" ("objgrp_member_id");
Alter table "objgrp" add  foreign key ("objgrp_member_id") references "object" ("obj_id") on update restrict on delete restrict;
Create index "IX_relationship26" on "rule_from" ("obj_id");
Alter table "rule_from" add  foreign key ("obj_id") references "object" ("obj_id") on update restrict on delete restrict;
Create index "IX_relationship28" on "rule_to" ("obj_id");
Alter table "rule_to" add  foreign key ("obj_id") references "object" ("obj_id") on update restrict on delete restrict;
Create index "IX_Relationship65" on "changelog_object" ("old_obj_id");
Alter table "changelog_object" add  foreign key ("old_obj_id") references "object" ("obj_id") on update restrict on delete restrict;
Create index "IX_Relationship66" on "changelog_object" ("new_obj_id");
Alter table "changelog_object" add  foreign key ("new_obj_id") references "object" ("obj_id") on update restrict on delete restrict;
Create index "IX_Relationship105" on "objgrp_flat" ("objgrp_flat_id");
Alter table "objgrp_flat" add  foreign key ("objgrp_flat_id") references "object" ("obj_id") on update restrict on delete restrict;
Create index "IX_Relationship106" on "objgrp_flat" ("objgrp_flat_member_id");
Alter table "objgrp_flat" add  foreign key ("objgrp_flat_member_id") references "object" ("obj_id") on update restrict on delete restrict;
Create index "IX_relationship25" on "rule_from" ("rule_id");
Alter table "rule_from" add  foreign key ("rule_id") references "rule" ("rule_id") on update restrict on delete restrict;
Create index "IX_relationship31" on "rule_review" ("rule_id");
Alter table "rule_review" add  foreign key ("rule_id") references "rule" ("rule_id") on update restrict on delete restrict;
Create index "IX_relationship29" on "rule_service" ("rule_id");
Alter table "rule_service" add  foreign key ("rule_id") references "rule" ("rule_id") on update restrict on delete restrict;
Create index "IX_relationship27" on "rule_to" ("rule_id");
Alter table "rule_to" add  foreign key ("rule_id") references "rule" ("rule_id") on update restrict on delete restrict;
Create index "IX_Relationship72" on "changelog_rule" ("new_rule_id");
Alter table "changelog_rule" add  foreign key ("new_rule_id") references "rule" ("rule_id") on update restrict on delete restrict;
Create index "IX_Relationship73" on "changelog_rule" ("old_rule_id");
Alter table "changelog_rule" add  foreign key ("old_rule_id") references "rule" ("rule_id") on update restrict on delete restrict;
Create index "IX_Relationship97" on "rule_order" ("rule_id");
Alter table "rule_order" add  foreign key ("rule_id") references "rule" ("rule_id") on update restrict on delete restrict;
Create index "IX_Relationship182" on "temp_filtered_rule_ids" ("rule_id");
Alter table "temp_filtered_rule_ids" add  foreign key ("rule_id") references "rule" ("rule_id") on update restrict on delete restrict;
Create index "IX_relationship30" on "rule_service" ("svc_id");
Alter table "rule_service" add  foreign key ("svc_id") references "service" ("svc_id") on update restrict on delete restrict;
Create index "IX_relationship19" on "svcgrp" ("svcgrp_id");
Alter table "svcgrp" add  foreign key ("svcgrp_id") references "service" ("svc_id") on update restrict on delete restrict;
Create index "IX_relationship20" on "svcgrp" ("svcgrp_member_id");
Alter table "svcgrp" add  foreign key ("svcgrp_member_id") references "service" ("svc_id") on update restrict on delete restrict;
Create index "IX_Relationship74" on "changelog_service" ("new_svc_id");
Alter table "changelog_service" add  foreign key ("new_svc_id") references "service" ("svc_id") on update restrict on delete restrict;
Create index "IX_Relationship75" on "changelog_service" ("old_svc_id");
Alter table "changelog_service" add  foreign key ("old_svc_id") references "service" ("svc_id") on update restrict on delete restrict;
Create index "IX_Relationship118" on "svcgrp_flat" ("svcgrp_flat_id");
Alter table "svcgrp_flat" add  foreign key ("svcgrp_flat_id") references "service" ("svc_id") on update restrict on delete restrict;
Create index "IX_Relationship119" on "svcgrp_flat" ("svcgrp_flat_member_id");
Alter table "svcgrp_flat" add  foreign key ("svcgrp_flat_member_id") references "service" ("svc_id") on update restrict on delete restrict;
Create index "IX_relationship23" on "rule" ("action_id");
Alter table "rule" add  foreign key ("action_id") references "stm_action" ("action_id") on update restrict on delete restrict;
Create index "IX_relationship12" on "object" ("obj_color_id");
Alter table "object" add  foreign key ("obj_color_id") references "stm_color" ("color_id") on update restrict on delete restrict;
Create index "IX_relationship18" on "service" ("svc_color_id");
Alter table "service" add  foreign key ("svc_color_id") references "stm_color" ("color_id") on update restrict on delete restrict;
Create index "IX_Relationship52" on "usr" ("user_color_id");
Alter table "usr" add  foreign key ("user_color_id") references "stm_color" ("color_id") on update restrict on delete restrict;
Create index "IX_relationship6" on "device" ("dev_typ_id");
Alter table "device" add  foreign key ("dev_typ_id") references "stm_dev_typ" ("dev_typ_id") on update restrict on delete restrict;
Create index "IX_Relationship83" on "management" ("dev_typ_id");
Alter table "management" add  foreign key ("dev_typ_id") references "stm_dev_typ" ("dev_typ_id") on update restrict on delete restrict;
Create index "IX_relationship10" on "object" ("nattyp_id");
Alter table "object" add  foreign key ("nattyp_id") references "stm_nattyp" ("nattyp_id") on update restrict on delete restrict;
Create index "IX_relationship9" on "object" ("obj_typ_id");
Alter table "object" add  foreign key ("obj_typ_id") references "stm_obj_typ" ("obj_typ_id") on update restrict on delete restrict;
Create index "IX_relationship24" on "rule" ("track_id");
Alter table "rule" add  foreign key ("track_id") references "stm_track" ("track_id") on update restrict on delete restrict;
Create index "IX_Relationship33" on "service" ("ip_proto_id");
Alter table "service" add  foreign key ("ip_proto_id") references "stm_ip_proto" ("ip_proto_id") on update restrict on delete restrict;
Create index "IX_Relationship36" on "service" ("svc_typ_id");
Alter table "service" add  foreign key ("svc_typ_id") references "stm_svc_typ" ("svc_typ_id") on update restrict on delete restrict;
Create index "IX_Relationship37" on "object" ("zone_id");
Alter table "object" add  foreign key ("zone_id") references "zone" ("zone_id") on update restrict on delete restrict;
Create index "IX_Relationship90" on "rule" ("rule_from_zone");
Alter table "rule" add  foreign key ("rule_from_zone") references "zone" ("zone_id") on update restrict on delete restrict;
Create index "IX_Relationship91" on "rule" ("rule_to_zone");
Alter table "rule" add  foreign key ("rule_to_zone") references "zone" ("zone_id") on update restrict on delete restrict;
Create index "IX_Relationship50" on "usergrp" ("usergrp_id");
Alter table "usergrp" add  foreign key ("usergrp_id") references "usr" ("user_id") on update restrict on delete restrict;
Create index "IX_Relationship51" on "usergrp" ("usergrp_member_id");
Alter table "usergrp" add  foreign key ("usergrp_member_id") references "usr" ("user_id") on update restrict on delete restrict;
Create index "IX_Relationship79" on "changelog_user" ("new_user_id");
Alter table "changelog_user" add  foreign key ("new_user_id") references "usr" ("user_id") on update restrict on delete restrict;
Create index "IX_Relationship80" on "changelog_user" ("old_user_id");
Alter table "changelog_user" add  foreign key ("old_user_id") references "usr" ("user_id") on update restrict on delete restrict;
Create index "IX_Relationship95" on "rule_from" ("user_id");
Alter table "rule_from" add  foreign key ("user_id") references "usr" ("user_id") on update restrict on delete restrict;
Create index "IX_Relationship134" on "client_user" ("user_id");
Alter table "client_user" add  foreign key ("user_id") references "usr" ("user_id") on update restrict on delete restrict;
Create index "IX_Relationship149" on "usergrp_flat" ("usergrp_flat_id");
Alter table "usergrp_flat" add  foreign key ("usergrp_flat_id") references "usr" ("user_id") on update restrict on delete restrict;
Create index "IX_Relationship150" on "usergrp_flat" ("usergrp_flat_member_id");
Alter table "usergrp_flat" add  foreign key ("usergrp_flat_member_id") references "usr" ("user_id") on update restrict on delete restrict;
Create index "IX_Relationship59" on "import_service" ("control_id");
Alter table "import_service" add  foreign key ("control_id") references "import_control" ("control_id") on update restrict on delete restrict;
Create index "IX_Relationship60" on "import_object" ("control_id");
Alter table "import_object" add  foreign key ("control_id") references "import_control" ("control_id") on update restrict on delete restrict;
Create index "IX_Relationship61" on "import_rule" ("control_id");
Alter table "import_rule" add  foreign key ("control_id") references "import_control" ("control_id") on update restrict on delete restrict;
Create index "IX_Relationship62" on "import_user" ("control_id");
Alter table "import_user" add  foreign key ("control_id") references "import_control" ("control_id") on update restrict on delete restrict;
Create index "IX_Relationship68" on "changelog_object" ("control_id");
Alter table "changelog_object" add  foreign key ("control_id") references "import_control" ("control_id") on update restrict on delete restrict;
Create index "IX_Relationship76" on "changelog_service" ("control_id");
Alter table "changelog_service" add  foreign key ("control_id") references "import_control" ("control_id") on update restrict on delete restrict;
Create index "IX_Relationship77" on "changelog_user" ("control_id");
Alter table "changelog_user" add  foreign key ("control_id") references "import_control" ("control_id") on update restrict on delete restrict;
Create index "IX_Relationship78" on "changelog_rule" ("control_id");
Alter table "changelog_rule" add  foreign key ("control_id") references "import_control" ("control_id") on update restrict on delete restrict;
Create index "IX_Relationship96" on "rule_order" ("control_id");
Alter table "rule_order" add  foreign key ("control_id") references "import_control" ("control_id") on update restrict on delete restrict;
Create index "IX_Relationship107" on "objgrp_flat" ("import_created");
Alter table "objgrp_flat" add  foreign key ("import_created") references "import_control" ("control_id") on update restrict on delete restrict;
Create index "IX_Relationship108" on "objgrp_flat" ("import_last_seen");
Alter table "objgrp_flat" add  foreign key ("import_last_seen") references "import_control" ("control_id") on update restrict on delete restrict;
Create index "IX_Relationship120" on "objgrp" ("import_created");
Alter table "objgrp" add  foreign key ("import_created") references "import_control" ("control_id") on update restrict on delete restrict;
Create index "IX_Relationship121" on "objgrp" ("import_last_seen");
Alter table "objgrp" add  foreign key ("import_last_seen") references "import_control" ("control_id") on update restrict on delete restrict;
Create index "IX_Relationship122" on "svcgrp" ("import_created");
Alter table "svcgrp" add  foreign key ("import_created") references "import_control" ("control_id") on update restrict on delete restrict;
Create index "IX_Relationship123" on "svcgrp" ("import_last_seen");
Alter table "svcgrp" add  foreign key ("import_last_seen") references "import_control" ("control_id") on update restrict on delete restrict;
Create index "IX_Relationship124" on "svcgrp_flat" ("import_created");
Alter table "svcgrp_flat" add  foreign key ("import_created") references "import_control" ("control_id") on update restrict on delete restrict;
Create index "IX_Relationship125" on "svcgrp_flat" ("import_last_seen");
Alter table "svcgrp_flat" add  foreign key ("import_last_seen") references "import_control" ("control_id") on update restrict on delete restrict;
Create index "IX_Relationship132" on "import_zone" ("control_id");
Alter table "import_zone" add  foreign key ("control_id") references "import_control" ("control_id") on update restrict on delete restrict;
Create index "IX_Relationship151" on "usergrp_flat" ("import_created");
Alter table "usergrp_flat" add  foreign key ("import_created") references "import_control" ("control_id") on update restrict on delete restrict;
Create index "IX_Relationship152" on "usergrp_flat" ("import_last_seen");
Alter table "usergrp_flat" add  foreign key ("import_last_seen") references "import_control" ("control_id") on update restrict on delete restrict;
Create index "IX_Relationship153" on "usergrp" ("import_created");
Alter table "usergrp" add  foreign key ("import_created") references "import_control" ("control_id") on update restrict on delete restrict;
Create index "IX_Relationship154" on "usergrp" ("import_last_seen");
Alter table "usergrp" add  foreign key ("import_last_seen") references "import_control" ("control_id") on update restrict on delete restrict;
Create index "IX_Relationship166" on "rule" ("rule_create");
Alter table "rule" add  foreign key ("rule_create") references "import_control" ("control_id") on update restrict on delete restrict;
Create index "IX_Relationship167" on "rule" ("rule_last_seen");
Alter table "rule" add  foreign key ("rule_last_seen") references "import_control" ("control_id") on update restrict on delete restrict;
Create index "IX_Relationship168" on "rule_from" ("rf_create");
Alter table "rule_from" add  foreign key ("rf_create") references "import_control" ("control_id") on update restrict on delete restrict;
Create index "IX_Relationship169" on "rule_from" ("rf_last_seen");
Alter table "rule_from" add  foreign key ("rf_last_seen") references "import_control" ("control_id") on update restrict on delete restrict;
Create index "IX_Relationship170" on "rule_to" ("rt_create");
Alter table "rule_to" add  foreign key ("rt_create") references "import_control" ("control_id") on update restrict on delete restrict;
Create index "IX_Relationship171" on "rule_to" ("rt_last_seen");
Alter table "rule_to" add  foreign key ("rt_last_seen") references "import_control" ("control_id") on update restrict on delete restrict;
Create index "IX_Relationship172" on "rule_service" ("rs_create");
Alter table "rule_service" add  foreign key ("rs_create") references "import_control" ("control_id") on update restrict on delete restrict;
Create index "IX_Relationship173" on "rule_service" ("rs_last_seen");
Alter table "rule_service" add  foreign key ("rs_last_seen") references "import_control" ("control_id") on update restrict on delete restrict;
Create index "IX_Relationship174" on "object" ("obj_create");
Alter table "object" add  foreign key ("obj_create") references "import_control" ("control_id") on update restrict on delete restrict;
Create index "IX_Relationship175" on "object" ("obj_last_seen");
Alter table "object" add  foreign key ("obj_last_seen") references "import_control" ("control_id") on update restrict on delete restrict;
Create index "IX_Relationship176" on "service" ("svc_create");
Alter table "service" add  foreign key ("svc_create") references "import_control" ("control_id") on update restrict on delete restrict;
Create index "IX_Relationship177" on "service" ("svc_last_seen");
Alter table "service" add  foreign key ("svc_last_seen") references "import_control" ("control_id") on update restrict on delete restrict;
Create index "IX_Relationship178" on "zone" ("zone_create");
Alter table "zone" add  foreign key ("zone_create") references "import_control" ("control_id") on update restrict on delete restrict;
Create index "IX_Relationship179" on "zone" ("zone_last_seen");
Alter table "zone" add  foreign key ("zone_last_seen") references "import_control" ("control_id") on update restrict on delete restrict;
Create index "IX_Relationship183" on "temp_mgmid_importid_at_report_time" ("control_id");
Alter table "temp_mgmid_importid_at_report_time" add  foreign key ("control_id") references "import_control" ("control_id") on update restrict on delete restrict;
Create index "IX_Relationship185" on "import_changelog" ("control_id");
Alter table "import_changelog" add  foreign key ("control_id") references "import_control" ("control_id") on update restrict on delete restrict;
Create index "IX_Relationship202" on "report" ("start_import_id");
Alter table "report" add  foreign key ("start_import_id") references "import_control" ("control_id") on update restrict on delete restrict;
Create index "IX_Relationship203" on "report" ("stop_import_id");
Alter table "report" add  foreign key ("stop_import_id") references "import_control" ("control_id") on update restrict on delete restrict;
Create index "IX_Relationship136" on "request_object_change" ("log_obj_id");
Alter table "request_object_change" add  foreign key ("log_obj_id") references "changelog_object" ("log_obj_id") on update restrict on delete restrict;
Create index "IX_Relationship63" on "changelog_object" ("import_admin");
Alter table "changelog_object" add  foreign key ("import_admin") references "isoadmin" ("isoadmin_id") on update restrict on delete restrict;
Create index "IX_Relationship69" on "changelog_service" ("import_admin");
Alter table "changelog_service" add  foreign key ("import_admin") references "isoadmin" ("isoadmin_id") on update restrict on delete restrict;
Create index "IX_Relationship70" on "changelog_user" ("import_admin");
Alter table "changelog_user" add  foreign key ("import_admin") references "isoadmin" ("isoadmin_id") on update restrict on delete restrict;
Create index "IX_Relationship71" on "changelog_rule" ("import_admin");
Alter table "changelog_rule" add  foreign key ("import_admin") references "isoadmin" ("isoadmin_id") on update restrict on delete restrict;
Create index "IX_Relationship109" on "changelog_object" ("doku_admin");
Alter table "changelog_object" add  foreign key ("doku_admin") references "isoadmin" ("isoadmin_id") on update restrict on delete restrict;
Create index "IX_Relationship110" on "changelog_service" ("doku_admin");
Alter table "changelog_service" add  foreign key ("doku_admin") references "isoadmin" ("isoadmin_id") on update restrict on delete restrict;
Create index "IX_Relationship111" on "changelog_user" ("doku_admin");
Alter table "changelog_user" add  foreign key ("doku_admin") references "isoadmin" ("isoadmin_id") on update restrict on delete restrict;
Create index "IX_Relationship112" on "changelog_rule" ("doku_admin");
Alter table "changelog_rule" add  foreign key ("doku_admin") references "isoadmin" ("isoadmin_id") on update restrict on delete restrict;
Create index "IX_Relationship159" on "object" ("last_change_admin");
Alter table "object" add  foreign key ("last_change_admin") references "isoadmin" ("isoadmin_id") on update restrict on delete restrict;
Create index "IX_Relationship161" on "rule" ("last_change_admin");
Alter table "rule" add  foreign key ("last_change_admin") references "isoadmin" ("isoadmin_id") on update restrict on delete restrict;
Create index "IX_Relationship162" on "service" ("last_change_admin");
Alter table "service" add  foreign key ("last_change_admin") references "isoadmin" ("isoadmin_id") on update restrict on delete restrict;
Create index "IX_Relationship163" on "usr" ("last_change_admin");
Alter table "usr" add  foreign key ("last_change_admin") references "isoadmin" ("isoadmin_id") on update restrict on delete restrict;
Create index "IX_Relationship81" on "error_log" ("error_id");
Alter table "error_log" add  foreign key ("error_id") references "error" ("error_id") on update restrict on delete restrict;
Create index "IX_Relationship139" on "request_service_change" ("log_svc_id");
Alter table "request_service_change" add  foreign key ("log_svc_id") references "changelog_service" ("log_svc_id") on update restrict on delete restrict;
Create index "IX_Relationship145" on "request_user_change" ("log_usr_id");
Alter table "request_user_change" add  foreign key ("log_usr_id") references "changelog_user" ("log_usr_id") on update restrict on delete restrict;
Create index "IX_Relationship142" on "request_rule_change" ("log_rule_id");
Alter table "request_rule_change" add  foreign key ("log_rule_id") references "changelog_rule" ("log_rule_id") on update restrict on delete restrict;
Create index "IX_Relationship93" on "usr" ("usr_typ_id");
Alter table "usr" add  foreign key ("usr_typ_id") references "stm_usr_typ" ("usr_typ_id") on update restrict on delete restrict;
Create index "IX_Relationship194" on "reporttyp_client_map" ("report_typ_id");
Alter table "reporttyp_client_map" add  foreign key ("report_typ_id") references "stm_report_typ" ("report_typ_id") on update restrict on delete restrict;
Create index "IX_Relationship201" on "report" ("report_typ_id");
Alter table "report" add  foreign key ("report_typ_id") references "stm_report_typ" ("report_typ_id") on update restrict on delete restrict;
Create index "IX_Relationship137" on "request_object_change" ("request_id");
Alter table "request_object_change" add  foreign key ("request_id") references "request" ("request_id") on update restrict on delete restrict;
Create index "IX_Relationship140" on "request_service_change" ("request_id");
Alter table "request_service_change" add  foreign key ("request_id") references "request" ("request_id") on update restrict on delete restrict;
Create index "IX_Relationship143" on "request_rule_change" ("request_id");
Alter table "request_rule_change" add  foreign key ("request_id") references "request" ("request_id") on update restrict on delete restrict;
Create index "IX_Relationship146" on "request_user_change" ("request_id");
Alter table "request_user_change" add  foreign key ("request_id") references "request" ("request_id") on update restrict on delete restrict;
Create index "IX_Relationship155" on "changelog_object" ("change_type_id");
Alter table "changelog_object" add  foreign key ("change_type_id") references "stm_change_type" ("change_type_id") on update restrict on delete restrict;
Create index "IX_Relationship156" on "changelog_service" ("change_type_id");
Alter table "changelog_service" add  foreign key ("change_type_id") references "stm_change_type" ("change_type_id") on update restrict on delete restrict;
Create index "IX_Relationship157" on "changelog_user" ("change_type_id");
Alter table "changelog_user" add  foreign key ("change_type_id") references "stm_change_type" ("change_type_id") on update restrict on delete restrict;
Create index "IX_Relationship158" on "changelog_rule" ("change_type_id");
Alter table "changelog_rule" add  foreign key ("change_type_id") references "stm_change_type" ("change_type_id") on update restrict on delete restrict;
Create index "IX_Relationship181" on "request" ("request_type_id");
Alter table "request" add  foreign key ("request_type_id") references "request_type" ("request_type_id") on update restrict on delete restrict;
Alter table "role_to_user" add  foreign key ("role_id") references "role" ("role_id") on update restrict on delete cascade;
Alter table "role_to_user" add  foreign key ("user_id") references "isoadmin" ("isoadmin_id") on update restrict on delete cascade;
Alter table "role_to_device" add  foreign key ("role_id") references "role" ("role_id") on update restrict on delete cascade;
Alter table "role_to_device" add  foreign key ("device_id") references "device" ("dev_id") on update restrict on delete cascade;

/* Create Groups */
Create group "secuadmins";
Create group "dbbackupusers";
Create group "configimporters";
Create group "reporters";
Create group "isoadmins";

/* Create Users */
Create user "itsecorg";
Create user "dbbackup";
Create user "isoimporter";


/* Add Users To Groups */
Alter group "dbbackupusers" add user "dbbackup";
Alter group "configimporters" add user "isoimporter";
Alter group "isoadmins" add user "itsecorg";

/* Create Group Permissions */

/*  grants for all (implicit) sequences */

GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO group "secuadmins";
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT USAGE, SELECT ON SEQUENCES TO group "secuadmins";

GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO group "configimporters";
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT USAGE, SELECT ON SEQUENCES TO group "configimporters";

GRANT SELECT ON ALL SEQUENCES IN SCHEMA public TO group "dbbackupusers";
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT SELECT ON SEQUENCES TO group "dbbackupusers";

GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO group "reporters";
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT USAGE, SELECT ON SEQUENCES TO group "reporters";

GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO group "isoadmins";
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT USAGE, SELECT ON SEQUENCES TO group "isoadmins";

/* Group permissions on tables */

-- general grants:

Grant ALL on ALL tables in SCHEMA public to group isoadmins;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON TABLES TO group isoadmins;

Grant select on ALL TABLES in SCHEMA public to group dbbackupusers;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT SELECT ON TABLES TO group dbbackupusers;

Grant select on ALL TABLES in SCHEMA public to group configimporters;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT SELECT ON TABLES TO group configimporters;

Grant select on ALL TABLES in SCHEMA public to group reporters;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT SELECT ON TABLES TO group reporters;

Grant select on ALL TABLES in SCHEMA public to group secuadmins;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT SELECT ON TABLES TO group secuadmins;

-- granular grants:

-- config importers:
Grant update on "device" to group "configimporters";
Grant update on "management" to group "configimporters";
Grant update,insert on "object" to group "configimporters";
Grant update,insert on "objgrp" to group "configimporters";
Grant update,insert on "rule" to group "configimporters";
Grant update,insert on "rule_from" to group "configimporters";
Grant update,insert on "rule_review" to group "configimporters";
Grant update,insert on "rule_service" to group "configimporters";
Grant update,insert on "rule_to" to group "configimporters";
Grant update,insert on "service" to group "configimporters";
Grant update,insert on "svcgrp" to group "configimporters";
Grant update,insert on "usr" to group "configimporters";
Grant update,insert on "usergrp" to group "configimporters";
Grant insert on "changelog_object" to group "configimporters";
Grant insert on "changelog_service" to group "configimporters";
Grant insert on "changelog_user" to group "configimporters";
Grant insert on "changelog_rule" to group "configimporters";
Grant insert on "error_log" to group "configimporters";
Grant update on "rule_order" to group "configimporters";
Grant update,insert on "objgrp_flat" to group "configimporters";
Grant update,insert on "svcgrp_flat" to group "configimporters";
Grant update,insert on "client_user" to group "configimporters";

Grant ALL on "import_service" to group "configimporters";
Grant ALL on "import_object" to group "configimporters";
Grant ALL on "import_user" to group "configimporters";
Grant ALL on "import_rule" to group "configimporters";
Grant ALL on "import_control" to group "configimporters";
Grant ALL on "import_zone" to group "configimporters";
Grant update,insert on "usergrp_flat" to group "configimporters";
Grant ALL on "import_changelog" to group "configimporters";


-- secuadmins:

Grant update on "isoadmin" to group "secuadmins";
Grant update on "isoadmin" to group "reporters";
Grant update,insert on "changelog_object" to group "secuadmins";
Grant update,insert on "changelog_service" to group "secuadmins";
Grant update,insert on "changelog_user" to group "secuadmins";
Grant update,insert on "changelog_rule" to group "secuadmins";
Grant update,insert on "error_log" to group "secuadmins";
Grant insert on "report" to group "secuadmins";
Grant ALL on "request" to group "secuadmins";
Grant ALL on "request_object_change" to group "secuadmins";
Grant ALL on "request_service_change" to group "secuadmins";
Grant ALL on "request_rule_change" to group "secuadmins";
Grant ALL on "request_user_change" to group "secuadmins";
Grant ALL on "temp_table_for_client_filtered_rule_ids" to group "secuadmins";
Grant ALL on "client_username" to group "secuadmins";
Grant ALL on "temp_filtered_rule_ids" to group "secuadmins";
Grant ALL on "temp_mgmid_importid_at_report_time" to group "secuadmins";

-- reporters:

Grant insert on "error_log" to group "reporters";
Grant insert on "report" to group "reporters";
Grant ALL on "temp_table_for_client_filtered_rule_ids" to group "reporters";
Grant ALL on "temp_filtered_rule_ids" to group "reporters";
Grant ALL on "temp_mgmid_importid_at_report_time" to group "reporters";



/* Group permissions on views */

/* Group permissions on procedures */

