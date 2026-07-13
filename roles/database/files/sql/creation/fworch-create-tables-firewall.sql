-- firewall schema: normalized firewall configuration tables (rules, network objects, services, users, zones)
-- moved here from the public schema (see issue #4793)

create schema firewall;

-- make the firewall schema resolvable without explicit qualification for unqualified references
-- in functions, triggers and views. current_database() is used so no db name templating is required.
DO $$
BEGIN
    EXECUTE format('ALTER DATABASE %I SET search_path = %s', current_database(), '"$user", public, firewall');
END $$;

Create table firewall.nw_object
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
	"removed" BIGINT,
	"flow_nwobj_id" BIGINT,
	"flow_nwgrp_id" BIGINT,
	"flow_active" BOOLEAN NOT NULL Default FALSE,
 primary key ("obj_id")
);

Create table firewall.nw_object_group
(
	"objgrp_id" BIGINT NOT NULL,
	"objgrp_member_id" BIGINT NOT NULL,
	"import_created" BIGINT NOT NULL,
	"active" Boolean NOT NULL Default TRUE,
	"negated" Boolean NOT NULL Default FALSE,
	"removed" BIGINT,
 primary key ("objgrp_id","objgrp_member_id")
);

Create table firewall.nw_service
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
	"removed" BIGINT,
	"flow_svcobj_id" BIGINT,
	"flow_svcgrp_id" BIGINT,
	"flow_active" BOOLEAN NOT NULL Default FALSE,
 primary key ("svc_id")
);

Create table firewall.nw_service_group
(
	"svcgrp_id" BIGINT NOT NULL,
	"svcgrp_member_id" BIGINT NOT NULL,
	"import_created" BIGINT NOT NULL,
	"active" Boolean NOT NULL Default TRUE,
	"negated" Boolean NOT NULL Default FALSE,
	"removed" BIGINT,
 primary key ("svcgrp_id","svcgrp_member_id")
);

Create table firewall.zone
(
	"zone_id" SERIAL,
	"zone_create" BIGINT NOT NULL,
	"mgm_id" Integer NOT NULL,
	"zone_name" Varchar NOT NULL,
	"active" Boolean NOT NULL Default TRUE,
	"removed" BIGINT,
 primary key ("zone_id")
);

Create table firewall.nw_user
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
	"user_comment" Text,
	"user_uid" Text,
	"user_firstname" Varchar,
	"user_lastname" Varchar,
	"last_change_admin" Integer,
	"tenant_id" Integer,
	"removed" BIGINT
);

Create table firewall.nw_user_group
(
	"usergrp_id" BIGINT,
	"usergrp_member_id" BIGINT,
	"import_created" BIGINT NOT NULL,
	"active" Boolean NOT NULL Default TRUE,
	"removed" BIGINT,
 primary key ("usergrp_id","usergrp_member_id")
);

Create table firewall.rule
(
	"rule_id" BIGSERIAL,
	"last_change_admin" Integer,
	"rule_name" Varchar,
	"mgm_id" Integer NOT NULL,
	"parent_rule_id" BIGINT,
	"parent_rule_type" smallint,
	"active" Boolean NOT NULL Default TRUE,
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
	"rule_action" Text NOT NULL,
	"rule_track" Text NOT NULL,
	"rule_installon" Varchar,
	"rule_time" Varchar,
	"rule_comment" Text,
	"rule_head_text" Text,
	"rule_implied" Boolean NOT NULL Default FALSE,
	"rule_create" BIGINT NOT NULL,
	"dev_id" Integer,
	"rule_custom_fields" jsonb,
	"access_rule" BOOLEAN Default TRUE,
	"nat_rule" BOOLEAN Default FALSE,
	"xlate_rule" BIGINT,
	"is_global" BOOLEAN DEFAULT FALSE NOT NULL,
	"rulebase_id" Integer NOT NULL,
	"removed" BIGINT,
	"flow_access_id" BIGINT,
	"rule_src_zone" Text,
	"rule_dst_zone" Text,
	primary key ("rule_id")
);

-- rule_metadata contains rule related data that does not change when the rule itself is changed
Create table firewall.rule_metadata
(
	"rule_metadata_id" BIGSERIAL,
	"rule_uid" Text NOT NULL,
	"mgm_id" Integer NOT NULL,
	"rule_created" BIGINT NOT NULL,
	"rule_first_hit" Timestamp,
	"rule_last_hit" Timestamp,
	"rule_hit_counter" BIGINT,
	"removed" BIGINT,
 primary key ("rule_metadata_id"),
 constraint "rule_metadata_mgm_id_rule_uid_unique" unique ("mgm_id", "rule_uid"));

Create table firewall.parent_rule_type
(
	"id" smallint NOT NULL,
	"name" Varchar NOT NULL,
 primary key ("id")
);

-- adding direct link tables rule_nw_[service|object|user]_resolved to make report object export easier
Create table firewall.rule_nw_service_resolved
(
	"mgm_id" INT,
	"rule_id" BIGINT NOT NULL,
	"svc_id" BIGINT NOT NULL,
	"created" BIGINT NOT NULL,
	"removed" BIGINT,
 primary key ("mgm_id","rule_id","svc_id","created")
);

Create table firewall.rule_nw_object_resolved
(
	"mgm_id" INT,
	"rule_id" BIGINT NOT NULL,
	"obj_id" BIGINT NOT NULL,
	"created" BIGINT NOT NULL,
	"removed" BIGINT,
 primary key ("mgm_id","rule_id","obj_id","created")
);

Create table firewall.rule_nw_user_resolved
(
	"mgm_id" INT,
	"rule_id" BIGINT NOT NULL,
	"user_id" BIGINT NOT NULL,
	"created" BIGINT NOT NULL,
	"removed" BIGINT,
 primary key ("mgm_id","rule_id","user_id","created")
);

Create table firewall.rule_from
-- needs separate primary key as user_id can be null
(
	"rule_from_id" BIGSERIAL PRIMARY KEY,
	"rule_id" BIGINT NOT NULL,
	"obj_id" BIGINT NOT NULL,
	"user_id" BIGINT,
	"active" Boolean NOT NULL Default TRUE,
	"negated" Boolean NOT NULL Default FALSE,
	"rf_create" BIGINT NOT NULL,
	"removed" BIGINT
);

Create table firewall.rule_service
(
	"rule_id" BIGINT NOT NULL,
	"svc_id" BIGINT NOT NULL,
	"active" Boolean NOT NULL Default TRUE,
	"rs_create" BIGINT NOT NULL,
	"negated" Boolean NOT NULL Default FALSE,
	"removed" BIGINT,
 primary key ("rule_id","svc_id")
);

Create table firewall.rule_to
-- needs separate primary key as user_id can be null
(
	"rule_to_id" BIGSERIAL PRIMARY KEY,
	"rule_id" BIGINT NOT NULL,
	"obj_id" BIGINT NOT NULL,
	"user_id" BIGINT,
	"rt_create" BIGINT NOT NULL,
	"active" Boolean NOT NULL Default TRUE,
	"negated" Boolean NOT NULL Default FALSE,
	"removed" BIGINT
);

Create table firewall.rulebase
(
	"id" SERIAL primary key,
	"name" Varchar NOT NULL,
	"uid" Varchar NOT NULL,
	"mgm_id" Integer NOT NULL,
	"is_global" BOOLEAN DEFAULT FALSE NOT NULL,
	"created" BIGINT,
	"removed" BIGINT
);

Create table firewall.rulebase_link
(
	"id" SERIAL primary key,
	"gw_id" Integer,
	"from_rulebase_id" Integer, -- either from_rulebase_id or from_rule_id must be SET or the is_initial flag
	"from_rule_id" BIGINT,
	"to_rulebase_id" Integer NOT NULL,
	"link_type" Integer,
	"is_initial" BOOLEAN DEFAULT FALSE,
	"is_global" BOOLEAN DEFAULT FALSE,
	"is_section" BOOLEAN DEFAULT TRUE,
	"created" BIGINT,
	"removed" BIGINT
);

Create Table firewall.rule_enforced_on_gateway
(
	"rule_id" Integer NOT NULL,
	"dev_id" Integer,  --  NULL if rule is available for all gateways of its management
	"created" BIGINT,
	"removed" BIGINT
);

--crosstabulation rule zone for source
Create table firewall.rule_from_zone
(
	"rule_id" BIGINT NOT NULL,
	"zone_id" Integer NOT NULL,
	"created" BIGINT NOT NULL,
	"removed" BIGINT,
	primary key (rule_id, zone_id, created)
);

--crosstabulation rule zone for destination
Create table firewall.rule_to_zone
(
	"rule_id" BIGINT NOT NULL,
	"zone_id" Integer NOT NULL,
	"created" BIGINT NOT NULL,
	"removed" BIGINT,
	primary key (rule_id, zone_id, created)
);

create table firewall.rule_time
(
    rule_time_id BIGSERIAL PRIMARY KEY,
    rule_id BIGINT,
    time_obj_id BIGINT,
    created BIGINT,
    removed BIGINT
);
