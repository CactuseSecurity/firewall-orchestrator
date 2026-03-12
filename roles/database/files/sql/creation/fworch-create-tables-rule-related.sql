
Create table "rule"
(
	"rule_id" BIGSERIAL,
	"last_change_admin" Integer,
	"rule_name" Varchar,
	"mgm_id" Integer NOT NULL,
	"parent_rule_id" BIGINT,
	"parent_rule_type" smallint,
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
	"rule_create" BIGINT NOT NULL,
	"rule_last_seen" BIGINT NOT NULL,
	"dev_id" Integer,
	"rule_custom_fields" jsonb,
	"access_rule" BOOLEAN Default TRUE,
	"nat_rule" BOOLEAN Default FALSE,
	"xlate_rule" BIGINT,
	"is_global" BOOLEAN DEFAULT FALSE NOT NULL,
	"rulebase_id" Integer NOT NULL,
	"removed" BIGINT,
	primary key ("rule_id")
);

-- rule_metadata contains rule related data that does not change when the rule itself is changed
Create table "rule_metadata"
(
	"rule_metadata_id" BIGSERIAL,
	"rule_uid" Text NOT NULL,
	"mgm_id" Integer NOT NULL,
	"rule_created" BIGINT NOT NULL,
	"rule_first_hit" Timestamp,
	"rule_last_hit" Timestamp,
	"rule_hit_counter" BIGINT,
	"removed" BIGINT,
 primary key ("rule_metadata_id") 
);

Create table "parent_rule_type"
(
	"id" smallint NOT NULL,
	"name" Varchar NOT NULL,
 primary key ("id")
);

-- adding direct link tables rule_[svc|nwobj|user]_resolved to make report object export easier
Create table "rule_svc_resolved"
(
	"mgm_id" INT,
	"rule_id" BIGINT NOT NULL,
	"svc_id" BIGINT NOT NULL,
	"created" BIGINT NOT NULL,
	"removed" BIGINT,
 primary key ("mgm_id","rule_id","svc_id","created")
);

Create table "rule_nwobj_resolved"
(
	"mgm_id" INT,
	"rule_id" BIGINT NOT NULL,
	"obj_id" BIGINT NOT NULL,
	"created" BIGINT NOT NULL,
	"removed" BIGINT,
 primary key ("mgm_id","rule_id","obj_id","created")
);

Create table "rule_user_resolved"
(
	"mgm_id" INT,
	"rule_id" BIGINT NOT NULL,
	"user_id" BIGINT NOT NULL,
	"created" BIGINT NOT NULL,
	"removed" BIGINT,
 primary key ("mgm_id","rule_id","user_id","created")
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
	"rf_create" BIGINT NOT NULL,
	"rf_last_seen" BIGINT NOT NULL,
	"removed" BIGINT
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
	"negated" Boolean NOT NULL Default FALSE,
	"removed" BIGINT
);

Create table "rulebase"
(
	"id" SERIAL primary key,
	"name" Varchar NOT NULL,
	"uid" Varchar NOT NULL,
	"mgm_id" Integer NOT NULL,
	"is_global" BOOLEAN DEFAULT FALSE NOT NULL,
	"created" BIGINT,
	"removed" BIGINT
);

Create table "rulebase_link"
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

Create Table "rule_enforced_on_gateway" 
(
	"rule_id" Integer NOT NULL,
	"dev_id" Integer,  --  NULL if rule is available for all gateways of its management
	"created" BIGINT,
	"removed" BIGINT
);

--crosstabulation rule zone for source
Create table "rule_from_zone"
(
	"rule_id" BIGINT NOT NULL,
	"zone_id" Integer NOT NULL,
	"created" BIGINT NOT NULL,
	"removed" BIGINT,
	primary key (rule_id, zone_id, created)
);

--crosstabulation rule zone for destination
Create table "rule_to_zone"
(
	"rule_id" BIGINT NOT NULL,
	"zone_id" Integer NOT NULL,
	"created" BIGINT NOT NULL,
	"removed" BIGINT,
	primary key (rule_id, zone_id, created)
);
