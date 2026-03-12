
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
	"dev_id" Integer,
	"change_type_id" Integer NOT NULL Default 3,
	"security_relevant" Boolean NOT NULL Default TRUE,
	"change_request_info" Varchar,
	"change_time" Timestamp,
	"unique_name" Varchar,
 primary key ("log_rule_id")
);

Create table "changelog_owner"
(
	"log_owner_id" BIGSERIAL,
	"control_id" BIGINT NOT NULL,
	"new_owner_id" BIGINT Constraint "changelog_owner_new_rule_id_constraint" Check ((change_action='D' AND new_owner_id IS NULL) OR NOT new_owner_id IS NULL),
	"old_owner_id" BIGINT Constraint "changelog_owner_old_rule_id_constraint" Check ((change_action='I' AND old_owner_id IS NULL) OR NOT old_owner_id IS NULL),
	"abs_change_id" BIGINT NOT NULL Default nextval('public.abs_change_id_seq'::text) UNIQUE,
	"change_action" Char(1) NOT NULL,
	"source_id" Varchar,
	"security_relevant" Boolean NOT NULL Default TRUE,
 primary key ("log_owner_id")
);

Create table "stm_change_type"
(
	"change_type_id" Integer,
	"change_type_name" Varchar,
 primary key ("change_type_id")
);
